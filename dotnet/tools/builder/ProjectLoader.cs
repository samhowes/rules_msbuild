using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;

namespace RulesMSBuild.Tools.Builder
{
    public class ProjectLoader
    {
        private readonly string _entryProjectPath;
        private readonly BuildCache _cache;
        
        public ProjectInstance EntryProject { get; set; }

        public ProjectLoader(string entryProjectPath, BuildCache cache)
        {
            _entryProjectPath = entryProjectPath;
            _cache = cache;
        }

        public ProjectInstance Load(ProjectCollection projectCollection)
        {
            var _ = new ProjectGraph(
                _entryProjectPath,
                projectCollection,
                CreateProjectInstance);
            
            return EntryProject;
        }
        
        private ProjectInstance CreateProjectInstance(string projectPath, Dictionary<string, string> globalProperties, ProjectCollection projectCollection)
        {
            var project = _cache.LoadProject(projectPath);
            if (project == null)
            {
                project = new ProjectInstance(
                    projectPath,
                    globalProperties,
                    "Current",
                    projectCollection);

                if (_cache.Project == null && project.FullPath == _entryProjectPath)
                {
                    _cache.Project = project;
                    EntryProject = project;
                }
            }
            else
            {
                project.LateInitialize(projectCollection.ProjectRootElementCache, null);
            }
            
            return project;
        }

        public void Initialize(string cacheManifestPath)
        {
            _cache.Initialize(cacheManifestPath);
                
        }
    }
}