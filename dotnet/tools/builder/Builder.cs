#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using Microsoft.Build.Logging;
using RulesMSBuild.Tools.Builder.Diagnostics;
using RulesMSBuild.Tools.Builder.MSBuild;
using static RulesMSBuild.Tools.Builder.BazelLogger;

namespace RulesMSBuild.Tools.Builder
{
    public class Builder
    {
        private readonly BuildContext _context;
        private readonly string _action;
        private BuildManager _buildManager;
        private readonly IBazelMsBuildLogger _msbuildLog;
        private readonly TargetGraph? _targetGraph;
        private readonly BuildCache _cache;
        private readonly ProjectLoader _loader;
        public readonly PathMapper PathMapper;
        private BuildParameters _buildParameters;

        public Builder(BuildContext context, IBazelMsBuildLogger? msbuildLog = null)
        {
            _context = context;
            _action = _context.Command.Action.ToLower();
            _buildManager = BuildManager.DefaultBuildManager;

            _targetGraph = context.TargetGraph;
            PathMapper = new PathMapper(context.Bazel.OutputBase, _context.Bazel.ExecRoot);
            _cache = new BuildCache(context.Bazel.Label, PathMapper, new Files(), _buildManager);
            _loader = new ProjectLoader(_context.ProjectFile, _cache, PathMapper, _targetGraph);
            _msbuildLog = msbuildLog ?? new BazelMsBuildLogger(
                m => Console.Out.Write(m),
                _context.DiagnosticsEnabled ? LoggerVerbosity.Normal : LoggerVerbosity.Quiet,
                (m) => PathMapper.ToBazel(m)
                , _targetGraph!);
        }

        public int Build()
        {
            Debug("$exec_root: " + _context.Bazel.ExecRoot);
            Debug("$output_base: " + _context.Bazel.OutputBase);
            ProjectCollection? projectCollection = null;
            BuildResultCode? result = null;
            try
            {
                projectCollection = BeginBuild();

                result = ExecuteBuild(projectCollection);

                EndBuild(result!.Value);
            
            }
            catch
            {
                try
                {
                    _buildManager!.EndBuild();
                }
                catch
                {
                    //ignored
                }
                
                throw;
            }
            finally
            {
                _buildManager!.Dispose();
                _buildManager = null;
                projectCollection?.Dispose();
            }

            return (int) result;
        }

        public ProjectCollection BeginBuild()
        {
            // GlobalProjectCollection loads EnvironmentVariables on Init. We use ExecRoot in the project files, we 
            // can't use MSBuildStartupDirectory because NuGet Restore uses a static graph restore which starts up a 
            // new process in the directory of the project file. We could set ExecRoot in the ProjectCollection Global
            // properties, but then we'd have to manage its value in the ConfigCache of the build manager later on.
            // Setting it here allows the project file to read it for paths and we don't have to clear it later.
            _context.SetEnvironment();

            var loggers = new List<ILogger>() {_msbuildLog};
            if (_context.DiagnosticsEnabled)
            {
                var path = _context.OutputPath(_context.Bazel.Label.Name + ".binlog");
                Debug($"added binlog {path}");
                var binlog = new BinaryLogger() {Parameters = path};
                loggers.Add(binlog);
            }

            _cache.Initialize(_context.LabelPath(".cache_manifest"));

            var pc = new ProjectCollection(_context.MSBuild.GlobalProperties, loggers,
                ToolsetDefinitionLocations.Default);
            
            _buildParameters = new BuildParameters(pc)
            {
                EnableNodeReuse = false,
                DetailedSummary = true,
                Loggers = pc.Loggers,
                ResetCaches = false,
                LogTaskInputs = _msbuildLog.Verbosity == LoggerVerbosity.Diagnostic,
                ProjectLoadSettings = _context.DiagnosticsEnabled ? ProjectLoadSettings.RecordEvaluatedItemElements 
                    : ProjectLoadSettings.Default,
                // cult-copy
                ToolsetDefinitionLocations =
                    ToolsetDefinitionLocations.ConfigurationFile |
                    ToolsetDefinitionLocations.Registry,
                ProjectRootElementCache = pc.ProjectRootElementCache,
                // ProjectCacheDescriptor = ProjectCacheDescriptor.FromInstance(
                //     new CachePlugin(_targetGraph, _cache),
                //     new[] {new ProjectGraphEntryPoint(_context.ProjectFile)}, 
                //     null, null),
                
                // InputResultsCacheFiles = cacheFiles.ToArray(),
            };
            _buildManager.BeginBuild(_buildParameters);
            if (_msbuildLog.HasError)
            {
                Console.WriteLine("Failed to initialize build manager, please file an issue.");
            }

            if (_targetGraph != null)
            {
                ReadCache();
            }

            return pc;
        }

        private void ReadCache()
        {
            var configCache =
                ((IBuildComponentHost) _buildManager).GetComponent(BuildComponentType.ConfigCache) as IConfigCache;
            var resultsCache =
                ((IBuildComponentHost) _buildManager).GetComponent(BuildComponentType.ResultsCache) as ResultsCacheWithOverride;

            if (resultsCache == null) return;
            
            
            var @overrideProperty =
                typeof(ResultsCacheWithOverride).GetField("_override", BindingFlags.NonPublic | BindingFlags.Instance);

            var @override = @overrideProperty!.GetValue(resultsCache) as IResultsCache;

            if (@override == null) return;
            
            foreach (var result in @override!)
            {
                var config = configCache![result.ConfigurationId];
                var path = PathMapper.ToBazel(config.ProjectFullPath);
                var cluster = _targetGraph!.GetOrAddCluster(path);
                foreach (var targetResult in result.ResultsByTarget)
                {
                    var node = cluster.GetOrAdd(targetResult.Key);
                    if (node.Name == "_PublishBuildAlternative")
                    {
                        
                    }
                    node.FromCache = true;
                }
            }
        }

        private BuildResultCode ExecuteBuild(ProjectCollection projectCollection)
        {
            if (_action == "pack")
            {
                var runfilesManifest = new FileInfo(_context.LabelPath(".runfiles_manifest"));
                if (runfilesManifest.Exists)
                {
                    WriteRunfilesProps(File.ReadAllLines(runfilesManifest.FullName));
                }
            }

            // don't load the project ahead of time, otherwise the evaluation won't be included in the binlog output
            var source = new TaskCompletionSource<BuildResultCode>();
            var flags = BuildRequestDataFlags.ReplaceExistingProjectInstance;

            // our restore outputs are relative to the project directory
            Environment.CurrentDirectory = _context.ProjectDirectory;

            var project = _loader.Load(projectCollection);

            if (!ValidateTfm(project))
                return BuildResultCode.Failure;
            
            if (_action == "restore")
            {
                if (!Directory.Exists(_context.MSBuild.RestoreDir))
                {
                    Directory.CreateDirectory(_context.MSBuild.RestoreDir);
                }
                
                WriteBazelProps();
            }
            
            if (false)
            {
                var graphData = new GraphBuildRequestData(
                    new ProjectGraphEntryPoint(_context.ProjectFile, projectCollection.GlobalProperties),
                    _context.MSBuild.Targets,
                    null,
                    flags);
                var submission = _buildManager.PendBuildRequest(graphData);

                submission.ExecuteAsync(submission =>
                {
                    var result = submission.BuildResult.OverallResult;
                    if (!ValidateTfm(_cache.Project))
                        result = BuildResultCode.Failure;

                    // if (result == BuildResultCode.Success)
                        // _cache!.RecordResult(submission.BuildResult);
                        
                    source.SetResult(result);
                }, new object());
            }
            else
            {
                var data = new BuildRequestData(
                    project,
                    _context.MSBuild.Targets, null, flags
                );

                if (_action == "pack")
                {
                    CloneConfiguration(data, project);
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

            }

            var result = source.Task.GetAwaiter().GetResult();

            return result;
        }

        private void CloneConfiguration(BuildRequestData data, ProjectInstance project)
        {
            var buildRequestConfiguration = new BuildRequestConfiguration(data, _buildParameters.DefaultToolsVersion);
            var actualConfiguration = _cache.ConfigCache!.GetMatchingConfiguration(buildRequestConfiguration);
            
            project.GlobalPropertiesDictionary.Set(ProjectPropertyInstance.Create("TargetFramework", _context.Tfm));

            var clonedId = _buildManager.GetNewConfigurationId();
            var clonedConfiguration = buildRequestConfiguration.ShallowCloneWithNewId(clonedId);
            _cache.ConfigCache.AddConfiguration(clonedConfiguration);

            var actualResults = _cache.ResultsCache!.GetResultsForConfiguration(actualConfiguration.ConfigurationId);

            _cache.ResultsCache.AddResult(
                new BuildResult(
                    actualResults,
                    BuildEventContext.InvalidSubmissionId,
                    clonedId,
                    BuildRequest.InvalidGlobalRequestId,
                    BuildRequest.InvalidGlobalRequestId,
                    BuildRequest.InvalidNodeRequestId
                ));
        }

        private void WriteRunfilesProps(string[] runfilesEntries)
        {
            var items = new List<XElement>(runfilesEntries.Length);
            var cwd = Directory.GetCurrentDirectory();
            foreach (var entry in runfilesEntries)
            {
                var parts = entry.Split(' ');
                var manifestPath = parts[0];
                var filePath = Path.Combine(cwd, parts[1]);

                items.Add(new XElement("None",
                    new XAttribute("Include", filePath),
                    new XElement("Pack", "true"),
                    new XElement("PackagePath", $"content/runfiles/{manifestPath}")));
            }

            var xml =
                new XElement("Project",
                    new XElement("ItemGroup", items));

            var path = _context.ProjectExtensionPath(".runfiles.props");
            
            WriteXml(xml, path);
        }

        private bool ValidateTfm(ProjectInstance? project)
        {
            var actualTfm = project?.GetProperty("TargetFramework")?.EvaluatedValue ?? "";
            if (actualTfm != _context.Tfm)
            {
                Error(
                    $"Bazel expected TargetFramework {_context.Tfm}, but {_context.WorkspacePath(_context.ProjectFile)} is " +
                    $"configured to use TargetFramework '{actualTfm}'. Refusing to build as this will " +
                    $"produce unreachable output. Please reconfigure the project and/or BUILD file.");
                {
                    return false;
                }
            }

            return true;
        }

        private void WriteBazelProps()
        {
            var props =
                _context.ProjectBazelProps.Select((pair) => new XElement(pair.Key, pair.Value));

            var bazelPropsPath = _context.ProjectExtensionPath(".bazel.props");
            
            var xml =
                new XElement("Project",
                    new XElement("PropertyGroup", props));
            WriteXml(xml, bazelPropsPath);
        }

        private void WriteXml(XElement xml, string path)
        {
            using var writer = XmlWriter.Create(path, 
                new XmlWriterSettings()
                {
                    OmitXmlDeclaration = true,
                    Indent = true
                });
            xml.Save(writer);
            writer.Flush();
        }

        private void EndBuild(BuildResultCode result)
        {
            if (result == BuildResultCode.Success)
                _cache.Save(_context.LabelPath(".cache"));

            _buildManager.EndBuild();

            if (_targetGraph != null)
            {
                File.WriteAllText(_context.LabelPath(".dot"), _targetGraph.ToDot());
            }

            if (result != BuildResultCode.Success) return;


            var saveEvaluation = false;
            switch (_action)
            {
                case "restore":
                    saveEvaluation = true;
                    FixRestoreOutputs();
                    break;
                case "build":
                {
                    // the restore sandbox doesn't have access to the source files, only project files, so we re-save 
                    // the evaluation with the updated items for this phase of the build.
                    saveEvaluation = true;
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

                        File.WriteAllLines(_context.OutputPath(_context.Tfm, "runfiles.info"), new string[]
                        {
                            // first line is the expected location of the runfiles directory from the assembly location
                            $"../{basename}.runfiles",
                            // second line is the origin workspace (nice to have)
                            _context.Bazel.Label.Workspace,
                            // third is the package (nice to have)
                            _context.Bazel.Label.Package
                        });
                    }

                    break;
                }
            }
            
            if (saveEvaluation)
                _cache.SaveProject(_context.OutputPath(Path.GetFileName(_context.ProjectFile) +$".{_action}.cache"));
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
    }
}