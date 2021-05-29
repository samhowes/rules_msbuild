#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        private readonly BuildContext _context;
        private readonly string _action;
        private readonly BuildManager _buildManager;
        private readonly MsBuildCacheManager _cacheManager;
        private readonly BazelMsBuildLogger _msbuildLog;

        public Builder(BuildContext context)
        {
            _context = context;
            _action = _context.Command.Action.ToLower();
            _buildManager = BuildManager.DefaultBuildManager;
            _cacheManager = new MsBuildCacheManager(_buildManager, _context.Bazel.ExecRoot);
            _msbuildLog = new BazelMsBuildLogger(
                _context.DiagnosticsEnabled ? LoggerVerbosity.Normal : LoggerVerbosity.Quiet,
                _context.Bazel.OutputBase);
        }

        public int Build()
        {
            var projectCollection = BeginBuild();

            var project = LoadProject(projectCollection);
            if (project == null) return 1;

            var result = ExecuteBuild(project);

            EndBuild(result);

            return (int) result.OverallResult;
        }

        private void EndBuild(BuildResult result)
        {
            if (_context.MSBuild.PostProcessCaches && result.OverallResult == BuildResultCode.Success)
            {
                _cacheManager.AfterExecuteBuild();
            }

            _buildManager.EndBuild();
 
            if (result.OverallResult == BuildResultCode.Success)
            {
                if (_action == "restore")
                {
                    // FixRestoreOutputs();
                }

                if (_action == "build" && _context.IsTest)
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
            }
        }

        private BuildResult ExecuteBuild(ProjectInstance project)
        {
            // this is a *Build* Request, NOT a *Graph* build request
            // build request only builds a single project in isolation, and any cache misses are considered errors.
            // loading up the project graph to begin with enables some other optimizations. 
            var data = new BuildRequestData(
                project,
                _context.MSBuild.Targets,
                null,
                // replace the existing config that we'll load from cache
                // not setting this results in MSBuild setting a global unique property to protect against 
                // https://github.com/dotnet/msbuild/issues/1748
                // todo: update above comment
                // setting to replace means that publish will discard item groups that were previoiusly built, 
                // resulting in publish not publishing content items.
                
                // Keep the project items that we have discovered for publish so publish doesn't do a re-build.
                BuildRequestDataFlags.None
            );

            var submission = _buildManager.PendBuildRequest(data);

            var source = new TaskCompletionSource<bool>();
            submission.ExecuteAsync(_ => source.SetResult(true), new object());
            source.Task.GetAwaiter().GetResult();
            var result = submission.BuildResult;
            return result;
        }

        private ProjectInstance? LoadProject(ProjectCollection projectCollection)
        {
            var noWarn = "NU1603;MSB3277";
            var globalProperties = new Dictionary<string, string>
            {
                ["BazelBuild"] = "true",
                ["ImportDirectoryBuildProps"] = "true",
                // ["EnableDefaultItems"] = "false",
                // ["EnableDefaultContentItems"] = "false",
                // ["EnableDefaultCompileItems"] = "false",
                // ["EnableDefaultEmbeddedResourceItems"] = "false",
                // ["EnableDefaultNoneItems"] = "false",
            };
            if (_action == "restore")
            {
                // we aren't using restore's cache files in the Build actions, so different global properties are fine

                // this is auto-set by NuGet.targets in Restore when restoring a referenced project. If we don't set it
                // ahead of time, there will be a cache miss on the restored project.
                // https://github.com/NuGet/NuGet.Client/blob/21e2a87537cd9655b7f6599af013d447aa058e29/src/NuGet.Core/NuGet.Build.Tasks/NuGet.targets#L69
                globalProperties["ExcludeRestorePackageImports"] = "true";
                // enables a faster nuget restore compatible with isolated builds
                // https://github.com/NuGet/NuGet.Client/blob/21e2a87537cd9655b7f6599af013d447aa058e29/src/NuGet.Core/NuGet.Build.Tasks/NuGet.targets#L1310
                globalProperties["RestoreUseStaticGraphEvaluation"] = "true";
            } 
            else if (_action == "publish")
            {
                // Setting this as a global property invalidates the input cache files from the build action.
                // MSBuild will do that anyway because it's going to load a config from the cache that matches the entry
                // project, but MSBuild does *not* serialize project state after a build so that
                // BuildRequestConfiguration instance will not have a ProjectInstance attached to it, and MSBuild will 
                // assume it ran into https://github.com/dotnet/msbuild/issues/1748, and set a global "Dummy" property
                // to explicitly invalidate the cache. We can set BuildRequestDataFlags.ReplaceExistingProjectInstance
                // to not invalidate the cache, but then publish won't have the right items calculated (at least
                // Content items will be missing), and we won't get the publish output we expect. 
                // To get the caching we want we'd have to somehow persist ProjectInstance to disk from the build action
                // which appears to be possible via the ITranslatable interface, but all of that code has `internal` 
                // visibility in the MSBuild assembly, and there is no one single method that we can target to persist
                // it to disk, but a collection of methods and classes. Might be doable with more knowledge of their 
                // codebase, but seems rather brittle and hacky with the knowledge I currently have.
                
                // tl;dr: we get a performance hit because we have to re-evaluate the project file, but for now, this is
                // how we get the full proper output.
                
                globalProperties["NoBuild"] = "true";
                // Publish re-executes the ResolveAssemblyReferences task, which uses the same .cache file as the build 
                // action. Since we'll have all the output from the build action, this file will be readonly in the 
                // sandbox. MSBuild opens this with Read+Write, so it will get an Access Denied exception and produce
                // a warning when trying to open that file. Suppress that warning. 
                noWarn += ";MSB3088";
            }

            globalProperties["NoWarn"] = noWarn;
            
            // load with a project graph so we get graph properties set on evaluation
            var graph = new ProjectGraph(_context.ProjectFile, globalProperties, projectCollection);

            if (_action == "publish" && graph.ProjectNodes.Count > 1)
            {
                throw new Exception(string.Join("\n",
                    graph.GetTargetLists(_context.MSBuild.Targets)
                    .Select(p => p.Key.ProjectInstance.FullPath + ": " + string.Join(",", p.Value))));
            }

            var project = graph.EntryPointNodes.Single().ProjectInstance;

            var actualTfm = project.GetProperty("TargetFramework")?.EvaluatedValue ?? "";
            if (actualTfm != _context.Tfm)
            {
                Error($"Bazel expected TargetFramework {_context.Tfm}, but {_context.WorkspacePath(_context.ProjectFile)} is " +
                      $"configured to use TargetFramework {actualTfm}. Refusing to build as this will " +
                      $"produce unreachable output. Please reconfigure the project and/or BUILD file.");
                return null;
            }

            return project;
        }

        private ProjectCollection BeginBuild()
        {
            // GlobalProjectCollection loads EnvironmentVariables on Init. We use ExecRoot in the project files, we 
            // can't use MSBuildStartupDirectory because NuGet Restore uses a static graph restore which starts up a 
            // new process in the directory of the project file. We could set ExecRoot in the ProjectCollection Global
            // properties, but then we'd have to manage its value in the ConfigCache of the build manager later on.
            // Setting it here allows the project file to read it for paths and we don't have to clear it later.
            _context.SetEnvironment();

            var pc = ProjectCollection.GlobalProjectCollection;

            BinaryLogger? binlog = null;
            // var loggers = pc.Loggers.ToList();
            if (_context.BinlogEnabled)
            {
                var path = _context.OutputPath(_context.Bazel.Label.Name + ".binlog");
                Debug($"added binlog {path}");
                binlog = new BinaryLogger() {Parameters = path};
                pc.RegisterLogger(binlog);
            }

            pc.RegisterLogger(_msbuildLog);
            var inputCaches = GetInputCaches();
            var parameters = new BuildParameters(pc)
            {
                EnableNodeReuse = false,
                Loggers = new ILogger[] {_msbuildLog, binlog!},
                DetailedSummary = true,
                IsolateProjects = true,
                ResetCaches = false,
                LogTaskInputs = _msbuildLog.Verbosity == LoggerVerbosity.Diagnostic,
                OutputResultsCacheFile = _context.LabelPath(".cache"),
                InputResultsCacheFiles = inputCaches, 
                ProjectLoadSettings = ProjectLoadSettings.RecordEvaluatedItemElements,
                // cult-copy
                ToolsetDefinitionLocations =
                    Microsoft.Build.Evaluation.ToolsetDefinitionLocations.ConfigurationFile |
                    Microsoft.Build.Evaluation.ToolsetDefinitionLocations.Registry,
            };
            _buildManager.BeginBuild(parameters);
            if (_msbuildLog.HasError)
            {
                Console.WriteLine("Failed to initialize build manager, please file an issue.");
            }
            else if (inputCaches.Any())
            {
                _cacheManager.BeforeExecuteBuild();
            }

            return pc;
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

                    path = Path.GetRelativePath(_context.Bazel.OutputDir, path);
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

        private void WaitForDebugger()
        {
            // Process currentProcess = Process.GetCurrentProcess();
            // Console.WriteLine($"Waiting for debugger to attach... ({currentProcess.MainModule.FileName} PID {currentProcess.Id})");
            // while (!Debugger.IsAttached)
            // {
            //     Thread.Sleep(100);
            // }
            // Console.WriteLine("debugger attached!");
            // Debugger.Break();
        }


        private string[] GetInputCaches()
        {
            var cacheManifest = _context.LabelPath(".input_caches");
            if (!File.Exists(cacheManifest)) return Array.Empty<string>();
            return File.ReadAllLines(cacheManifest);
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