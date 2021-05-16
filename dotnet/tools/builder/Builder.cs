#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
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

        public Builder(ProcessorContext context)
        {
            _context = context;
            _action = _context.Command.Action.ToLower();
            _buildManager = BuildManager.DefaultBuildManager;
            _cacheManager = new MsBuildCacheManager(_buildManager, _context.ExecRoot);
        }

        string CachePath(string projectPath, string? action = null)
            => ProjectPath(projectPath, action ?? _action, "cache");

        string BinlogPath(string projectPath) =>
            ProjectPath(projectPath, _action, "binlog");

        private string ProjectPath(params string[] parts) => string.Join(".", parts);


        public int Build()
        {
            var pc = ProjectCollection.GlobalProjectCollection;
            pc.RegisterLogger(new BazelMsBuildLogger(_context.BazelOutputBase));
            pc.SetGlobalProperty("ImportDirectoryBuildProps", "false");
            pc.SetGlobalProperty("NoWarn", "NU1603;MSB3277");
            if (_action == "restore")
            {
                // this one is auto-set by NuGet.targets in Restore when restoring a referenced project. If we don't set it
                // ahead of time, there will be a cache miss on the restored project.
                pc.SetGlobalProperty("ExcludeRestorePackageImports", "true");
            }

            var loggers = pc.Loggers.ToList();
            // todo(#51) disable when no build diagnostics are requested
            if (true)
            {
                var path = BinlogPath(Path.GetFullPath(_context.ProjectFile));
                Debug($"added binlog {path}");
                loggers.Add(new BinaryLogger() {Parameters = path});
            }

            var project = Project.FromFile(_context.ProjectFile, new ProjectOptions()
            {
                ProjectCollection = pc,
            });
            Console.WriteLine($"Project {project.FullPath} loaded");

            string[] targets;
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
                    break;
                default:
                    throw new ArgumentException($"Unknown action {_action}");
            }

            var inputCaches = GetInputCaches(project);
            var parameters = new BuildParameters(ProjectCollection.GlobalProjectCollection)
            {
                EnableNodeReuse = false,
                Loggers = loggers,
                DetailedSummary = true,
                IsolateProjects = true,
                OutputResultsCacheFile = CachePath(project.FullPath),
                InputResultsCacheFiles = inputCaches.ToArray(),
                // cult-copy
                ToolsetDefinitionLocations =
                    Microsoft.Build.Evaluation.ToolsetDefinitionLocations.ConfigurationFile |
                    Microsoft.Build.Evaluation.ToolsetDefinitionLocations.Registry,
            };
            var data = new BuildRequestData(
                project.CreateProjectInstance(),
                targets,
                null,
                // replace the existing config that we'll load from cache
                // not setting this results in MSBuild setting a global unique property to protect against 
                // https://github.com/dotnet/msbuild/issues/1748
                BuildRequestDataFlags.ReplaceExistingProjectInstance
            );


            _buildManager.BeginBuild(parameters);
            if (inputCaches.Any())
            {
                _cacheManager.BeforeExecuteBuild();
            }

            var submission = _buildManager.PendBuildRequest(data);

            var result = submission.Execute();

            if (result.OverallResult == BuildResultCode.Success)
            {
                _cacheManager.AfterExecuteBuild();    
            }
            
            _buildManager.EndBuild();
            return (int) result.OverallResult;
        }


        private  List<string> GetInputCaches(Project project)
        {
            var inputCaches = new List<string>();
            foreach (var reference in project.GetItems("ProjectReference"))
            {
                var path = reference.EvaluatedInclude;

                inputCaches.Add(CachePath(path));
            }

            return inputCaches;
        }
    }
}