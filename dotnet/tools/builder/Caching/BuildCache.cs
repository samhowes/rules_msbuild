using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using RulesMSBuild.Tools.Builder.Diagnostics;
using static RulesMSBuild.Tools.Builder.BazelLogger;

#pragma warning disable 8618

namespace RulesMSBuild.Tools.Builder.Caching
{
    public class BuildCache
    {
        public CacheManifest? Manifest;
        public ProjectInstance? Project;
        private readonly PathMapper _pathMapper;
        private readonly Files _files;
        public LabelResult Result;
        private readonly TargetGraph? _targetGraph;
        Func<int> _newConfigurationId;
        public readonly ConfigCache ConfigCache;
        public readonly ResultsCache ResultsCache;
        private readonly Dictionary<TargetResult, string> _originalResults = new ();

        public BuildCache(BazelContext.BazelLabel label, 
            PathMapper pathMapper, 
            Files files, 
            TargetGraph? targetGraph)
        {
            ResultsCache = new BazelResultCache();
            ConfigCache = new ConfigCache();
            Result = new LabelResult()
            {
                Label = new Label(label.Workspace, label.Package, label.Name),
                NewIds =new Dictionary<int, int>(),
                OriginalIds = new Dictionary<int, int>(),
            };
            _pathMapper = pathMapper;
            _files = files;
            _targetGraph = targetGraph;
        }

        public void Initialize(string manifestPath, BuildManager? buildManager)
        {
            int id = 0;
            _newConfigurationId = buildManager != null ? buildManager.GetNewConfigurationId : () => ++id;
            IConfigCache buildManagerConfigCache;
            if (!_files.Exists(manifestPath))
            {
                Debug("No input caches found");
                Manifest = new CacheManifest();
                Result.ConfigCache = ConfigCache;
                buildManagerConfigCache = ConfigCache;
            }
            else
            {
                var cacheManifestJson = _files.GetContents(manifestPath);
                var cacheManifest = JsonSerializer.Deserialize<CacheManifest>(cacheManifestJson,
                    new JsonSerializerOptions() {PropertyNameCaseInsensitive = true});
                Manifest = cacheManifest!;

                var (caches, cachesInOrder) = DeserializeCaches();
                AggregateCaches(cachesInOrder, caches);
                var withOverride = new ConfigCacheWithOverride(ConfigCache);
                buildManagerConfigCache = withOverride;
                // only save new configurations to the output cache
                Result.ConfigCache = withOverride.CurrentCache;
            }

            buildManager?.ReuseOldCaches(buildManagerConfigCache, ResultsCache);
        }

        public void CloneConfiguration(BuildRequestData data, string toolsVersion, ProjectInstance project)
        {
            var buildRequestConfiguration = new BuildRequestConfiguration(data, toolsVersion);
            var actualConfiguration = ConfigCache!.GetMatchingConfiguration(buildRequestConfiguration);
            var tfm = project.GetPropertyValue("TargetFramework");
            project.GlobalPropertiesDictionary.Set(ProjectPropertyInstance.Create("TargetFramework", tfm));

            var clonedId = _newConfigurationId();
            var clonedConfiguration = buildRequestConfiguration.ShallowCloneWithNewId(clonedId);
            ConfigCache.AddConfiguration(clonedConfiguration);

            var actualResults = ResultsCache!.GetResultsForConfiguration(actualConfiguration.ConfigurationId);

            ResultsCache.AddResult(
                new BuildResult(
                    actualResults,
                    BuildEventContext.InvalidSubmissionId,
                    clonedId,
                    BuildRequest.InvalidGlobalRequestId,
                    BuildRequest.InvalidGlobalRequestId,
                    BuildRequest.InvalidNodeRequestId
                ));
        }
        
        public (Dictionary<string, LabelResult> caches, List<LabelResult> cachesInOrder) DeserializeCaches()
        {
            var caches = new Dictionary<string, LabelResult>();
            var cachesInOrder = new List<LabelResult>();
            foreach (var cacheFile in Manifest!.Results)
            {
                LabelResult result = null!;
                // ReSharper disable once AccessToModifiedClosure
                DoTranslate(cacheFile, CreateReadTranslator, (t) => TranslateResult(ref result, t));
                caches[result.Label.ToString()] = result;
                cachesInOrder.Add(result);
            }

            return (caches, cachesInOrder);
        }

        public void AggregateCaches(List<LabelResult> cachesInOrder,
            Dictionary<string, LabelResult> caches)
        {
            foreach (var labelResult in cachesInOrder)
            {
                var configs = labelResult.ConfigCache.GetEnumerator().ToArray();
                var results = labelResult.Results;

                foreach (var config in configs)
                {
                    ErrorUtilities.VerifyThrow(ConfigCache!.GetMatchingConfiguration(config) == null,
                        "Input caches should not contain entries for the same configuration");
                    
                    labelResult.NewIds = new Dictionary<int, int>();
                    var newId = _newConfigurationId();
                    labelResult.NewIds[config.ConfigurationId] = newId;
                    Result.OriginalIds[newId] = config.ConfigurationId;
                    // record which label this configuration belongs to in case our results reference the config
                    Result.ConfigMap[newId] = labelResult.Label.ToString();

                    var newConfig = config.ShallowCloneWithNewId(newId);
                    newConfig.ResultsNodeId = Scheduler.InvalidNodeId;

                    // if we don't set this, msbuild will clear the configuration in BeginBuild.
                    newConfig.ExplicitlyLoaded = true;
                    ConfigCache.AddConfiguration(newConfig);
                }

                foreach (var result in results)
                {
                    int newConfigId;
                    if (labelResult.ConfigMap.TryGetValue(result.ConfigurationId, out var configSource))
                    {
                        var originalId = labelResult.OriginalIds[result.ConfigurationId];
                        // assume that bazel properly ordered the cache list in postorder in the depset
                        newConfigId = caches[configSource].NewIds[originalId];
                    }
                    else
                    {
                        newConfigId = labelResult.NewIds[result.ConfigurationId];
                    }

                    Cluster? cluster = null;
                    if (_targetGraph != null)
                    {
                        var config = ConfigCache![newConfigId];
                        var path = _pathMapper.ToBazel(config.ProjectFullPath);
                        cluster = _targetGraph!.GetOrAddCluster(path);
                    }

                    foreach (var targetResult in result.ResultsByTarget)
                    {
                        // quick and dirty way to figure out which results we built in this build
                        _originalResults[targetResult.Value] = targetResult.Key;
                        
                        if (cluster != null)
                        {
                            var node = cluster.GetOrAdd(targetResult.Key);
                            node.FromCache = true;
                        }
                    }

                    ResultsCache!.AddResult(
                        new BuildResult(
                            result,
                            BuildEventContext.InvalidSubmissionId,
                            newConfigId,
                            BuildRequest.InvalidGlobalRequestId,
                            BuildRequest.InvalidGlobalRequestId,
                            BuildRequest.InvalidNodeRequestId
                        ));
                }
            }
        }

        public void Save()
        {
            if (!string.IsNullOrEmpty(Manifest?.Output?.Project))
                SaveProject(_pathMapper.ToAbsolute(Manifest.Output.Project));
            if (!string.IsNullOrEmpty(Manifest?.Output?.Result))
                Save(_pathMapper.ToAbsolute(Manifest.Output.Result));
        }

        public void Save(string path)
        {
            FilterResults();
            DoTranslate(path, CreateWriteTranslator, (t) => TranslateResult(ref Result, t));
        }

        private void FilterResults()
        {
            var allResults = ResultsCache!.GetEnumerator().ToArray();
            IEnumerable<BuildResult> resultsToKeep;
            if (_originalResults.Any())
            {
                var list = new List<BuildResult>();
                foreach (var result in allResults)
                {
                
                    var targetNames = new List<string>();
                    foreach (var (targetName, targetResult) in result.ResultsByTarget)
                    {
                        if (_originalResults.ContainsKey(targetResult)) continue;
                        targetNames.Add(targetName);
                    }
                    if (targetNames.Count == result.ResultsByTarget.Count)
                        list.Add(result);
                    else
                        list.Add(new BuildResult(result, targetNames.ToArray()));
                }

                resultsToKeep = list;
            }
            else
            {
                resultsToKeep = allResults;
            }

            Result.Results = resultsToKeep.ToArray();
        }

        public void SaveProject(string path)
        {
            Project!.TranslateEntireState = true;
            DoTranslate(path, CreateWriteTranslator, (t) => TranslateProject(ref Project, t));
        }

        public ProjectInstance? LoadProject(string projectPath)
        {
            var manifestPath = _pathMapper.ToManifestPath(projectPath);

            string? cachePath = null;
            if (Manifest!.Projects?.TryGetValue(manifestPath, out cachePath) != true)
            {
                Debug($"Project cache miss: {manifestPath}");
                return null;
            }

            Debug($"Project cache hit: {manifestPath}");
            cachePath = _pathMapper.ToAbsolute(cachePath!);
            return LoadProjectImpl(cachePath);
        }

        public ProjectInstance? LoadProjectImpl(string cachePath)
        {
            ProjectInstance project = null!;
            DoTranslate(cachePath, CreateReadTranslator, t => TranslateProject(ref project, t));

            return project;
        }

        #region Translation

        private void TranslateProject(ref ProjectInstance project, ITranslator translator)
        {
            translator.Translate(ref project, ProjectInstance.FactoryForDeserialization);
        }

        private void TranslateResult(ref LabelResult result, ITranslator translator)
        {
            translator.Translate(ref result);
        }

        private void DoTranslate(string path, Func<Stream, ITranslator> createTranslator, Action<ITranslator> translate)
        {
            using var stream = createTranslator == CreateReadTranslator ? _files.OpenRead(path) : _files.Create(path);
            var translator = createTranslator(stream);
            translate(translator);
        }

        private ITranslator CreateReadTranslator(Stream stream)
        {
            return BinaryTranslator.GetReadTranslator(stream, null);
        }

        private ITranslator CreateWriteTranslator(Stream stream)
        {
            return BinaryTranslator.GetWriteTranslator(stream);
        }

        #endregion
    }
}