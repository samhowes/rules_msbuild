#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using RulesMSBuild.Tools.Builder.Caching;
using RulesMSBuild.Tools.Builder.Diagnostics;
using RulesMSBuild.Tools.Builder.MSBuild;
using static RulesMSBuild.Tools.Builder.BazelLogger;

namespace RulesMSBuild.Tools.Builder
{
    public class BuilderDependencies
    {
        public List<ILogger> Loggers;
        public IBazelMsBuildLogger BuildLog;
        public PathMapper PathMapper;
        public BuildCache Cache;
        public ProjectLoader ProjectLoader;
        public BuilderDependencies(BuildContext context, IBazelMsBuildLogger? buildLog = null)
        {
            PathMapper = new PathMapper(context.Bazel.OutputBase, context.Bazel.ExecRoot);
            Cache = new BuildCache(context.Bazel.Label, PathMapper, new Files(), context.TargetGraph);
            ProjectLoader = new ProjectLoader(context.ProjectFile, Cache, PathMapper, context.TargetGraph);
            BuildLog = buildLog ?? new BazelMsBuildLogger(
                m =>
                {
                    Console.Out.Write(m);
                    Console.Out.Flush();
                },
                context.DiagnosticsEnabled ? LoggerVerbosity.Normal : LoggerVerbosity.Quiet,
                (m) => PathMapper.ToBazel(m));

            Loggers = new List<ILogger>(){BuildLog};
            if (context.DiagnosticsEnabled)
            {
                var path = context.OutputPath(context.Bazel.Label.Name + ".binlog");
                Debug($"added binlog {path}");
                var binlog = new BinaryLogger() {Parameters = path};
                Loggers.Add(binlog);
            }

            if (context.TargetGraph != null)
            {
                Loggers.Add(new TargetGraphLogger(context.TargetGraph!, PathMapper));
            }
        }
    }
    
    public class Builder
    {
        private readonly BuildContext _context;
        private readonly BuilderDependencies _deps;
        private readonly string _action;
        private readonly TargetGraph? _targetGraph;
        private BuildParameters _buildParameters;
        private readonly BuildManager _buildManager;
        private readonly IBazelMsBuildLogger _log;

        public Builder(BuildContext context, BuilderDependencies deps)
        {
            _context = context;
            _deps = deps;
            _action = _context.Command.Action.ToLower();
            _buildManager = BuildManager.DefaultBuildManager;
            _targetGraph = context.TargetGraph;
            _log = deps.BuildLog;
        }

        public int Build()
        {
            Debug("$exec_root: " + _context.Bazel.ExecRoot);
            Debug("$output_base: " + _context.Bazel.OutputBase);
            ProjectInstance? project = null;
            try
            {
                project = BeginBuild();
                if (project == null) return -1;
                
                var result = ExecuteBuild(project);

                EndBuild(result);
                return (int) result;
            }
            catch (Exception ex)
            {
                var shouldThrow = true;
                if (ex is BazelException)
                {
                    _log.Error(ex.Message);
                    shouldThrow = false;
                }
                try
                {
                    _buildManager.EndBuild();
                }
                catch
                {
                    //ignored
                }

                if (shouldThrow)
                    throw;
                return 1;
            }
            finally
            {
                _buildManager.Dispose();
            }
        }

        public ProjectInstance? BeginBuild()
        {
            // GlobalProjectCollection loads EnvironmentVariables on Init. We use ExecRoot in the project files, we 
            // can't use MSBuildStartupDirectory because NuGet Restore uses a static graph restore which starts up a 
            // new process in the directory of the project file. We could set ExecRoot in the ProjectCollection Global
            // properties, but then we'd have to manage its value in the ConfigCache of the build manager later on.
            // Setting it here allows the project file to read it for paths and we don't have to clear it later.
            _context.SetEnvironment();

            if (!_deps.Cache.Initialize(_context.LabelPath(".cache_manifest"), _buildManager))
            {
                return null;
            }

            var pc = new ProjectCollection(
                _context.MSBuild.GlobalProperties, 
                _deps.Loggers,
                ToolsetDefinitionLocations.Default);
            
            
            // our restore outputs are relative to the project directory
            Environment.CurrentDirectory = _context.ProjectDirectory;

            ProjectInstance project;
            try
            {
                project = _deps.ProjectLoader.Load(pc);
            }
            catch (ProjectCacheMissException missingException)
            {
                Fail($"invalid ProjectReference",
                    $"ProjectReference: \"{_deps.PathMapper.ToBazel(missingException.ProjectPath)}\" is not " +
                    $"listed in the deps attribute of {_context.Bazel.Label}. Did you remember to " +
                    $"`bazel run //:gazelle` after updating your project file?");
                return null;
            }

            if (!ValidateTfm(project))
                return null;
            
            _buildParameters = new BuildParameters(pc)
            {
                EnableNodeReuse = false,
                DetailedSummary = true,
                Loggers = pc.Loggers,
                ResetCaches = false,
                MaxNodeCount = 1,
                LogTaskInputs = _context.DiagnosticsEnabled,
                ProjectLoadSettings = _context.DiagnosticsEnabled ? 
                    ProjectLoadSettings.RecordEvaluatedItemElements 
                    : ProjectLoadSettings.Default,
                // cult-copy
                ToolsetDefinitionLocations =
                    ToolsetDefinitionLocations.ConfigurationFile |
                    ToolsetDefinitionLocations.Registry,
                ProjectRootElementCache = pc.ProjectRootElementCache,
            };
            _buildManager.BeginBuild(_buildParameters);

            if (_deps.BuildLog.HasError)
            {
                Console.WriteLine("Failed to initialize build manager, please file an issue.");
                return null;
            }

            return project;
        }

        private BuildResultCode ExecuteBuild(ProjectInstance project)
        {
            var source = new TaskCompletionSource<BuildResultCode>();
            var flags = BuildRequestDataFlags.ReplaceExistingProjectInstance;
            
            var data = new BuildRequestData(
                project,
                _context.MSBuild.Targets, null, flags
            );

            switch (_action)
            {
                case "restore":
                    if (!Directory.Exists(_context.MSBuild.RestoreDir))
                    {
                        Directory.CreateDirectory(_context.MSBuild.RestoreDir);
                    }
                    new BazelPropsWriter().WriteProperties(
                        _context.ProjectExtensionPath(".bazel.props"),
                        _context.ProjectBazelProps);
                    break;
                case "pack":
                    var runfilesManifest = new FileInfo(_context.LabelPath(".runfiles_manifest"));
                    if (runfilesManifest.Exists)
                    {
                        foreach (var entry in File.ReadAllLines(runfilesManifest.FullName))
                        {
                            var parts = entry.Split(' ');
                            var manifestPath = parts[0];
                            var filePath = _deps.PathMapper.ToAbsolute(parts[1]);
                            project.AddItem("None", filePath, new[]
                            {
                                new KeyValuePair<string, string>("Pack", "true"),
                                new KeyValuePair<string, string>("PackagePath", $"content/runfiles/{manifestPath}"),
                            });
                        }
                    }
                    
                    
                    // the dll will be placed at    <root>/tools/<tfm>/any/<primaryName>.dll
                    // runfiles will be at          <root>/content/runfiles
                    var packDir = _context.OutputPath("pack");
                    Directory.CreateDirectory(packDir);
                    var path = Path.Combine(packDir, "runfiles.info");
                    WriteRunfilesInfo(path, "../../../content/runfiles", true);
                    project.AddItem("None", path, new[]
                    {
                        // new KeyValuePair<string, string>("What", "wow"),
                        new KeyValuePair<string, string>("Pack", "true"),
                        new KeyValuePair<string, string>("PackagePath", $"tools/{_context.Tfm}/any/"),
                    });
                    
                    // The default 'Pack' implementation by nuget sets a global property for the target framework
                    // this invalidates cache entries since they are keyed by ProjectFullPath + GlobalProperties
                    // We enforce a single target framework though, so this specification is not necessary
                    //
                    // additionally, it rebuilds targets that produce outputs, like writing to an Assembly References 
                    // cache file, and Bazel will have those files marked as ReadOnly, so MSBuild will fail the build because
                    // it can't write to that file.
                    
                    // to prevent rebuilding, we clone the configuration so MSBuild will reuse the results from previous
                    // builds.
                    _deps.Cache.CloneConfiguration(data, _buildParameters.DefaultToolsVersion, project);
                    break;
            }

            _buildManager.PendBuildRequest(data)
                .ExecuteAsync(submission =>
                {
                    var result = submission.BuildResult.OverallResult;

                    if (submission.BuildResult.Exception != null)
                    {
                        Error(submission.BuildResult.Exception.ToString());
                    }

                    source.SetResult(result);
                }, new object());
            
            var resultCode = source.Task.GetAwaiter().GetResult();

            return resultCode;
        }
        
        private bool ValidateTfm(ProjectInstance? project)
        {
            var actualTfm = project?.GetProperty("TargetFramework")?.EvaluatedValue ?? "";
            if (actualTfm != _context.Tfm)
            {
                Error(
                    $"Bazel expected TargetFramework {_context.Tfm}, but {_context.WorkspacePath(_context.ProjectFile)} " +
                    $"is configured to use TargetFramework '{actualTfm}'. Refusing to build as this will " +
                    $"produce unreachable output. Please reconfigure the project and/or BUILD file.");
                return false;
            }

            return true;
        }

        private void EndBuild(BuildResultCode result)
        {
            if (result == BuildResultCode.Success)
                _deps.Cache.Save();

            _buildManager.EndBuild();

            if (_targetGraph != null)
            {
                File.WriteAllText(_context.LabelPath(".dot"), _targetGraph.ToDot());
            }

            if (result != BuildResultCode.Success) return;

            switch (_action)
            {
                case "restore":
                    FixRestoreOutputs();
                    break;
                case "build":
                {
                    if (_context.IsTest)
                    {
                        // todo make this less hacky
                        var loggerPath = Path.Combine(
                            Path.GetDirectoryName(_context.NuGetConfig)!,
                            "packages/junitxml.testlogger/3.0.87/build/_common");
                        var tfmPath = Path.Combine(_context.MSBuild.OutputPath, _context.Tfm);
                        foreach (var dll in Directory.EnumerateFiles(loggerPath))
                        {
                            var filename = Path.GetFileName(dll);
                            File.Copy(dll, Path.Combine(tfmPath, filename));
                        }    
                    }

                    if (_context.IsExecutable)
                    {
                        var basename = _context.Bazel.Label.Name;
                        if (Path.DirectorySeparatorChar == '\\')
                        {
                            // there's not a great "IsWindows" method in c#
                            basename += ".exe";
                        }

                        WriteRunfilesInfo(_context.OutputPath(_context.Tfm, "runfiles.info"), $"../{basename}.runfiles", false);
                    }

                    break;
                }
            }
        }

        private void WriteRunfilesInfo(string outputPath, string expectedRelativePath, bool useDirectory)
        {
            File.WriteAllLines(outputPath, new string[]
            {
                // first line is the expected location of the runfiles directory from the assembly location
                expectedRelativePath,
                // second line is the origin workspace (nice to have)
                _context.Bazel.Label.Workspace,
                // third is the package (nice to have)
                _context.Bazel.Label.Package,
                // runfiles strategy to use
                useDirectory ? "directory" : "auto"
            });
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
            foreach (var fileName in Directory.EnumerateFiles(_context.MSBuild.BaseIntermediateOutputPath))
            {
                var target = _context.Bazel.OutputBase;

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

                    if (isJson)
                    {
                        path = Path.GetRelativePath(_context.ProjectDirectory, path);
                    }
                    else
                    {
                        path = Path.Combine("$(ExecRoot)", Path.GetRelativePath(_context.Bazel.ExecRoot, path));
                    }

                    if (needsEscaping)
                    {
                        path = Escape(path);
                    }

                    output.Write(path);
                }

                output.Write(contents[index..]);
                output.Flush();
            }
        }

        private void CopyFiles(string filesKey, string destinationDirectory, bool trimPackage = false)
        {
            // if (!_context.Command.NamedArgs.TryGetValue(filesKey, out var contentListString) ||
            //     contentListString == "") return;
            var contentListString = "";
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
                else if (trimPackage && filePath.StartsWith(_context.Bazel.Label.Package))
                {
                    destinationPath = filePath.Substring(_context.Bazel.Label.Package.Length + 1);
                }
                else
                {
                    destinationPath = Path.Combine(_context.Bazel.Label.Workspace, filePath);
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

        public void Error(string message) => BazelLogger.Error(_deps.PathMapper.ToBazel(message));
        public void Fail(string shortMessage, string message)
        {
            var projectPath = _deps.PathMapper.ToBazel(_context.ProjectFile);
            var summary = new StringBuilder()
                .AppendLine("__________________________________________________")
                .AppendLine($"Project \"{projectPath}\": (Build target(s)):")
                .AppendLine()
                .AppendLine($"{projectPath}(-1,-1): error BZ0001: {_deps.PathMapper.ToBazel(message)}");
            BazelLogger.Error(summary.ToString());
            /* proper error output from normal build that triggers IDE parsing:
0>__________________________________________________
0>Project "$exec_root/rules_msbuild/eng/tar/tar.csproj" (Build target(s)):
0>
0>$exec_root/rules_msbuild/eng/tar/Program.cs(62,30): Error CS1026 : ) expected
0>Done building project "tar.csproj" -- FAILED.
            */
        }
    }
}
