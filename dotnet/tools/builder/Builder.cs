#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            }

            return (int) overallResult;
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