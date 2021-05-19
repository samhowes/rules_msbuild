#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using Microsoft.Build.Logging;
using static MyRulesDotnet.Tools.Builder.BazelLogger;

namespace MyRulesDotnet.Tools.Builder
{
    public class Builder
    {
        private readonly ProcessorContext _context;
        private readonly string _action;
        private readonly BuildManager _buildManager;
        private readonly MsBuildCacheManager _cacheManager;
        private BazelMsBuildLogger _msbuildLog;

        private const string ContentKey = "content";
        private const string RunfilesKey = "runfiles";
        private const string RunfilesDirectoryKey = "runfiles_directory";

        public Builder(ProcessorContext context)
        {
            _context = context;
            _action = _context.Command.Action.ToLower();
            _buildManager = BuildManager.DefaultBuildManager;
            _cacheManager = new MsBuildCacheManager(_buildManager, _context.ExecRoot);
            _msbuildLog = new BazelMsBuildLogger(
                _context.DiagnosticsEnabled ? LoggerVerbosity.Normal : LoggerVerbosity.Quiet,
                _context.BazelOutputBase);
        }

        string CachePath(string projectPath, string? action = null)
            => ProjectPath(projectPath, action ?? _action, "cache");

        string BinlogPath(string projectPath) =>
            ProjectPath(projectPath, _action, "binlog");

        private string ProjectPath(params string[] parts) => string.Join(".", parts);


        public int Build()
        {
            // GlobalProjectCollection loads EnvironmentVariables on Init. We use ExecRoot in the project files, we 
            // can't use MSBuildStartupDirectory because NuGet Restore uses a static graph restore which starts up a 
            // new process in the directory of the project file. We could set ExecRoot in the ProjectCollection Global
            // properties, but then we'd have to manage its value in the ConfigCache of the build manager later on.
            // Setting it here allows the project file to read it for paths and we don't have to clear it later.
            Environment.SetEnvironmentVariable("ExecRoot", _context.ExecRoot);
            if (_action == "publish")
            {
                var relative = Path.GetRelativePath(Path.GetDirectoryName(_context.ProjectFile)!,
                    _context.OutputDirectory);
                Environment.SetEnvironmentVariable("PublishDir", relative);
            }


            var pc = ProjectCollection.GlobalProjectCollection;

            pc.RegisterLogger(_msbuildLog);
            var globalProperties = new Dictionary<string, string>
            {
                ["ImportDirectoryBuildProps"] = "false",
                ["NoWarn"] = "NU1603;MSB3277",
            };
            if (_action == "restore")
            {
                // this is auto-set by NuGet.targets in Restore when restoring a referenced project. If we don't set it
                // ahead of time, there will be a cache miss on the restored project.
                // https://github.com/NuGet/NuGet.Client/blob/21e2a87537cd9655b7f6599af013d447aa058e29/src/NuGet.Core/NuGet.Build.Tasks/NuGet.targets#L69
                globalProperties["ExcludeRestorePackageImports"] = "true";
                // only restore this project, bazel takes care of making sure other projects are restored
                globalProperties["RestoreRecursive"] = "false";
                // enables a faster nuget restore compatible with isolated builds
                // https://github.com/NuGet/NuGet.Client/blob/21e2a87537cd9655b7f6599af013d447aa058e29/src/NuGet.Core/NuGet.Build.Tasks/NuGet.targets#L1310
                globalProperties["RestoreUseStaticGraphEvaluation"] = "true";
            }

            var loggers = pc.Loggers.ToList();
            if (_context.BinlogEnabled)
            {
                var path = BinlogPath(Path.GetFullPath(_context.ProjectFile));
                Debug($"added binlog {path}");
                loggers.Add(new BinaryLogger() {Parameters = path});
            }

            var graph = new ProjectGraph(_context.ProjectFile, globalProperties, pc);

            List<string>? inputCaches = null;
            string[] targets;
            var skipAfterExecute = false;
            string? outputCache = null;
            switch (_action)
            {
                case "restore":
                    targets = new[] {"Restore"};
                    break;
                case "build":
                    targets = new[]
                    {
                        "GetTargetFrameworks", "Build", "GetCopyToOutputDirectoryItems", "GetNativeManifest"
                    };
                    inputCaches = GetInputCaches(graph);
                    outputCache = CachePath(_context.ProjectFile);
                    break;
                case "publish":
                    targets = new[] {"Publish"};
                    inputCaches = new List<string>() {CachePath(_context.ProjectFile, "build")};
                    skipAfterExecute = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown action {_action}");
            }


            var parameters = new BuildParameters(ProjectCollection.GlobalProjectCollection)
            {
                EnableNodeReuse = false,
                Loggers = loggers,
                DetailedSummary = true,
                IsolateProjects = true,
                OutputResultsCacheFile = outputCache,
                InputResultsCacheFiles = inputCaches?.ToArray(),
                // cult-copy
                ToolsetDefinitionLocations =
                    Microsoft.Build.Evaluation.ToolsetDefinitionLocations.ConfigurationFile |
                    Microsoft.Build.Evaluation.ToolsetDefinitionLocations.Registry,
            };
            var data = new GraphBuildRequestData(
                graph,
                targets,
                null,
                // replace the existing config that we'll load from cache
                // not setting this results in MSBuild setting a global unique property to protect against 
                // https://github.com/dotnet/msbuild/issues/1748
                BuildRequestDataFlags.ReplaceExistingProjectInstance
            );

            _buildManager.BeginBuild(parameters);
            if (_msbuildLog.HasError)
            {
                Console.WriteLine("Failed to initialize build manager, please file an issue.");
            }
            else if (inputCaches?.Any() == true)
            {
                _cacheManager.BeforeExecuteBuild();
            }

            var submission = _buildManager.PendBuildRequest(data);

            var result = submission.Execute();

            var overallResult = result.OverallResult;
            if (!skipAfterExecute && overallResult == BuildResultCode.Success)
            {
                _cacheManager.AfterExecuteBuild();
            }

            _buildManager.EndBuild();

            if (overallResult == BuildResultCode.Success)
            {
                CopyFiles(ContentKey, _context.OutputDirectory, true);
                if (_context.Command.NamedArgs.TryGetValue(RunfilesDirectoryKey, out var runfilesDirectory))
                {
                    CopyFiles(RunfilesKey, Path.Combine(_context.OutputDirectory, runfilesDirectory));
                }

                if (_action == "restore")
                {
                    FixRestoreOutputs();
                }
            }

            return (int) overallResult;
        }

        /// <summary>
        /// Restore writes absolute paths to project.assets.json and to .props and .targets files.
        /// We can't have absolute paths for these, because they will be re-used in future actions in a different
        /// sandbox or machine.
        /// For the assets file, we assume that the only build action that is looking at the file is MSBuild building
        /// the direct project. As such, the current directory will be the directory of this project file, so we'll
        /// make all the paths relative to the project file.
        /// For the xml files, we'll prepend MSBuildThisFileDirectory in case another project file is evaluating these
        /// files.
        /// </summary>
        private void FixRestoreOutputs()
        {
            var projectDir = Path.GetDirectoryName(Path.GetFullPath(_context.ProjectFile))!;


            var projectName = Path.GetFileName(_context.ProjectFile);
            var obj = Path.Combine(projectDir, "obj");
            var filesToKeep = new[] {".nuget.g.props", ".nuget.g.targets"}
                .Select(f => projectName + f)
                .Append("project.assets.json")
                .Select(f => Path.Combine(obj, f))
                .ToHashSet();

            foreach (var fileName in Directory.EnumerateFiles(obj))
            {
                if (!filesToKeep.Contains(fileName))
                {
                    // just to make sure MSBuild doesn't use them
                    File.Delete(fileName);
                    continue;
                }

                var target = _context.BazelOutputBase;

                var isJson = fileName.EndsWith("json");
                var needsEscaping = isJson && Path.DirectorySeparatorChar == '\\';

                string Escape(string s) => s.Replace(@"\", @"\\");

                if (needsEscaping)
                    target = Escape(target);

                var contents = File.ReadAllText(fileName);
                using var output = new StreamWriter(File.Open(fileName, FileMode.Truncate));
                var index = 0;
                for (;;)
                {
                    var thisIndex = contents.IndexOf(target, index, StringComparison.Ordinal);
                    if (thisIndex == -1) break;
                    output.Write(contents[index..thisIndex]);

                    if (contents[thisIndex..(thisIndex + "sandbox".Length)] == "sandbox")
                        thisIndex = contents.IndexOf("execroot", index, StringComparison.Ordinal);
                    
                    var endOfPath = contents.IndexOfAny(new[] {'"', ';', '<'}, thisIndex);
                
                    index = endOfPath;
                
                    var path = contents[thisIndex..endOfPath];
                
                    if (needsEscaping)
                        path = path.Replace(@"\\", @"\");
                
                    path = Path.GetRelativePath(projectDir, path);
                    if (needsEscaping)
                    {
                        path = Escape(path);
                    }
                    
                    if (!isJson)
                    {
                        path = Path.Combine("$(MSBuildThisFileDirectory)", 
                            "..", // one more to get out of the obj directory 
                            path);
                    }
                    output.Write(path);
                }
                
                output.Write(contents[index..]);
                output.Flush();
            }
        }


        private List<string> GetInputCaches(ProjectGraph graph)
        {
            var entry = graph.EntryPointNodes.Single();
            return graph.ProjectNodes
                .Where(n => n != entry)
                .Select(n => CachePath(n.ProjectInstance.FullPath))
                .ToList();
        }

        private void CopyFiles(string filesKey, string destinationDirectory, bool trimPackage = false)
        {
            if (!_context.Command.NamedArgs.TryGetValue(filesKey, out var contentListString) ||
                contentListString == "") return;
            var contentList = contentListString.Split(";");
            var createdDirectories = new HashSet<string>();
            foreach (var filePath in contentList)
            {
                var src = new FileInfo(filePath);
                string destinationPath;
                if (filePath.StartsWith("external/"))
                {
                    destinationPath = filePath.Substring("external/".Length);
                }
                else if (trimPackage && filePath.StartsWith(_context.Package))
                {
                    destinationPath = filePath.Substring(_context.Package.Length + 1);
                }
                else
                {
                    destinationPath = Path.Combine(_context.Workspace, filePath);
                }

                var dest = new FileInfo(Path.Combine(destinationDirectory, destinationPath));

                if (!dest.Exists || src.LastWriteTime > dest.LastWriteTime)
                {
                    if (!createdDirectories.Contains(dest.DirectoryName!))
                    {
                        Directory.CreateDirectory(dest.DirectoryName!);
                        createdDirectories.Add(dest.DirectoryName!);
                    }

                    src.CopyTo(dest.FullName, true);
                }
            }
        }
    }
}