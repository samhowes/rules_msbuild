using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using RulesMSBuild.Tools.Builder.Caching;
using RulesMSBuild.Tools.Builder.Diagnostics;

namespace RulesMSBuild.Tools.Builder
{
    public class ProjectLoader
    {
        private readonly string _entryProjectPath;
        private readonly BuildCache _cache;
        private readonly TargetGraph? _targetGraph;
        private PathMapper _pathMapper;

        public ProjectInstance EntryProject { get; set; }

        public ProjectLoader(string entryProjectPath, BuildCache cache, PathMapper pathMapper, TargetGraph? targetGraph = null)
        {
            _entryProjectPath = Path.GetFullPath(entryProjectPath);
            _cache = cache;
            _pathMapper = pathMapper;
            _targetGraph = targetGraph;
        }

        public ProjectInstance Load(ProjectCollection projectCollection)
        {
            // TaskItems do not save the absolute path, only the relative path from the ProjectDirectory
            // because of this, when we load a project from cache, the Full Path of task items will be re-evaluated
            // CreateProjectInstanceImpl Changes the directory to the just-loaded project so the ProjectGraph can get
            // the correct absolute paths of transitively referenced project files.
            // without this a dependency chain of `foo/bar/bam.csproj -> foo/bar.csproj -> foo.csproj` will break
            var originalDirectory = Environment.CurrentDirectory;
            var _ = new ProjectGraph(
                new []{new ProjectGraphEntryPoint(_entryProjectPath)},
                projectCollection,
                CreateProjectInstance,
                1, new CancellationTokenSource(1000000).Token);
            
            Environment.CurrentDirectory = originalDirectory;
            return EntryProject;
        }
        
        private ProjectInstance CreateProjectInstance(string projectPath, Dictionary<string, string> globalProperties, ProjectCollection projectCollection)
        {
            try
            {
                return CreateProjectInstanceImpl(projectPath, globalProperties, projectCollection);
            }
            catch (Exception ex)
            {
                if (ex is BazelException) throw;
                throw new Exception($"An exception occurred while loading project {projectPath}.", ex);
            }
        }

        private ProjectInstance CreateProjectInstanceImpl(string projectPath, Dictionary<string, string> globalProperties,
            ProjectCollection projectCollection)
        {
            var project = _cache.LoadProject(projectPath);
            if (project == null)
            {
                if (projectPath != _entryProjectPath)
                {
                    var manifestPath = _pathMapper.ToManifestPath(projectPath);
                    throw new BazelException(
                        $"Expected to find {projectPath} in cache. Only {_entryProjectPath} is expected to miss the " +
                        $"cache.\nManifestPath:{manifestPath}");
                }
                foreach (var (name, value) in projectCollection.GlobalProperties)
                {
                    globalProperties[name] = value;
                }
                project = new ProjectInstance(
                    projectPath,
                    globalProperties,
                    "Current",
                    projectCollection);
            }
            else
            {
                project.LateInitialize(projectCollection.ProjectRootElementCache, null);
            }
            Environment.CurrentDirectory = project.Directory;

            if (_cache.Project == null && project.FullPath == _entryProjectPath)
            {
                _cache.Project = project;
                EntryProject = project;
            }

            if (_targetGraph != null)
            {
                var path = _pathMapper.ToBazel(projectPath);
                var cluster = _targetGraph.GetOrAddCluster(path, null);
                void AddTargets(TargetGraph.Node thisTarget, string targetString, TargetBuiltReason reason)
                {
                    foreach (var beforeName in targetString.Split(";")
                        .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s)))
                    {
                        if (beforeName.StartsWith("$"))
                        {
                            var property = GetProperty(beforeName, project);
                            if (property == null)
                                continue;
                            AddTargets(thisTarget, property.EvaluatedValue, reason);
                            return;
                        }
                        var other = cluster!.GetOrAdd(beforeName);
                        TargetGraph.Node parent = null!;
                        TargetGraph.Node child = null!;
                        switch (reason)
                        {
                            case TargetBuiltReason.BeforeTargets:
                                parent = other;
                                child = thisTarget;
                                break;
                            
                            case TargetBuiltReason.DependsOn:
                            case TargetBuiltReason.AfterTargets:
                            default:
                                parent = thisTarget;
                                child = other;
                                break;
                        }

                        parent.AddDependency(child, reason);
                    }
                }
                
                foreach (var (targetName, target) in project.Targets)
                {
                    var thisTarget = cluster.GetOrAdd(targetName);
                    AddTargets(thisTarget, target.DependsOnTargets, TargetBuiltReason.DependsOn);
                    AddTargets(thisTarget, target.AfterTargets, TargetBuiltReason.AfterTargets);
                    AddTargets(thisTarget, target.BeforeTargets, TargetBuiltReason.BeforeTargets);

                    foreach (var buildTask in target.Tasks)
                    {
                        List<string>? projects = null;
                        switch (buildTask.Name.ToLower())
                        {
                            case "msbuild":
                                if (buildTask.Parameters.TryGetValue("Projects", out var projectsString))
                                {
                                    projects = projectsString.Split(";").ToList();
                                }
                                else
                                {
                                    throw new Exception(":(");
                                }

                                break;
                            case "calltarget":
                                projects = new List<string>() {"$(MSBuildProjectFullPath)"};
                                break;
                            default:
                                continue;
                        }

                        foreach (var projectValue in projects)
                        {
                            if (projectValue.Contains("RestoreGraph")) continue;
                            List<string>? defaultTargets = null;
                            Cluster targetCluster = cluster;
                            var first = projectValue[0];
                            if (projectValue[1] == ':') // windows
                            {
                                first = '/';
                            }
                            switch (first)
                            {
                                case '/':
                                case '%':
                                case '@':
                                    // these items likely won't have anything in them yet, since we haven't dont a build yet.
                                    // we'll make them a "meta" cluster
                                    var bazelPath = _pathMapper.ToBazel(projectValue);
                                    targetCluster = _targetGraph.GetOrAddCluster(bazelPath, null);
                                    break;
                                case '$':
                                    switch (projectValue)
                                    {
                                        case "$(MSBuildProjectFullPath)":
                                            defaultTargets = project.DefaultTargets;
                                            break;
                                        case "$(MSBuildThisFileFullPath)":
                                        {
                                            var builtProject = buildTask.FullPath;
                                            var sdkIndex = builtProject.ToLower().IndexOf("sdk");
                                            if (sdkIndex >= 0)
                                            {
                                                builtProject = builtProject[sdkIndex..];
                                            }

                                            targetCluster = _targetGraph.GetOrAddCluster(builtProject, null);
                                            break;
                                        }
                                        default:
                                            throw new Exception(":(");
                                            break;
                                    }
                                    break;
                                default:
                                    throw new Exception(":(");
                                    break;
                            }

                            if (!buildTask.Parameters.TryGetValue("Targets", out var targets))
                            {
                                if (defaultTargets == null) 
                                    throw new Exception(":(");
                                targets = string.Join(";", defaultTargets);
                            }

                            bool wtf = buildTask.Parameters.TryGetValue("Properties", out var properties);

                            foreach (var buildTargetName in targets.Split(";"))
                            {
                                var actual = ResolveTarget(buildTargetName, project);
                                var other = targetCluster.GetOrAdd(actual);
                                // other.Error = wtf;
                                if (wtf)
                                {
                                    // Console.WriteLine($"wtf: {thisTarget.Name} -> {other.Name}: {properties}");
                                }
                                thisTarget.AddDependency(other, null);

                                if (targetCluster != cluster)
                                {
                                    Darken(other);
                                    Darken(cluster.GetOrAdd(actual));
                                }
                            }
                        }
                    }
                }
            }

            return project;
        }

        private void Darken(TargetGraph.Node node, int depth = 0)
        {
            node.ReferencedExternally = true;
            if (depth > 0) return;
            
            foreach (var edge in node.Dependencies.Values.Cast<TargetGraph.Edge>())
            {
                // Darken(edge.To, depth++);
            }
        }

        private static ProjectPropertyInstance? GetProperty(string beforeName, ProjectInstance? project)
        {
            var propertyName = beforeName[2..^1];
            var property = project.Properties.SingleOrDefault(p => p.Name == propertyName);
            return property;
        }

        private static string ResolveTarget(string originalName, ProjectInstance? project)
        {
            var property = GetProperty(originalName, project);
            return property?.EvaluatedValue ?? originalName;
        }
    }

    public class BazelException : Exception
    {
        public BazelException(string message) : base(message)
        {
        }
    }
}
