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
using static RulesMSBuild.Tools.Builder.BazelLogger;

#pragma warning disable 8618

namespace RulesMSBuild.Tools.Builder
{
    public class Label : BazelContext.BazelLabel, ITranslatable
    {
        public Label(string workspace, string package, string name)
        :base(workspace, package, name)
        {}

        public Label(){} // constructor for deserialization

        public void Translate(ITranslator translator)
        {
            // we can't load the assembly until we have our bazel context
            // the bazel context creates the label
            // therefore bazelLabel can't implement ITranslator because we won't have SdkRoot yet
            // and won't have msbuild loaded yet.
            translator.Translate(ref Workspace);
            translator.Translate(ref Package);
            translator.Translate(ref Name);
        }
    }
    public class LabelResult : ITranslatable
    {
        public Label Label;
        public ConfigCache ConfigCache;
        public ResultsCache ResultsCache;
        public IDictionary<int, string> ConfigMap = new Dictionary<int, string>();
        public Dictionary<int, int> NewIds; // do not translate
        public IDictionary<int, int> OriginalIds;

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref Label);
            translator.Translate(ref ConfigCache);
            translator.Translate(ref ResultsCache);

            translator.TranslateDictionary(ref ConfigMap,
                (ITranslator t, ref int i) => t.Translate(ref i),
                (ITranslator t, ref string s) => t.Translate(ref s),
                c => new Dictionary<int, string>()
            );
            translator.TranslateDictionary(ref OriginalIds,
                (ITranslator t, ref int i) => t.Translate(ref i),
                (ITranslator t, ref int i) => t.Translate(ref i),
                c => new Dictionary<int, int>()
            );
        }
    }

    public class CacheManifest
    {
        public class BuildResultCache
        {
            public string Project { get; set; }
            public string Result { get; set; }
        }

        public BuildResultCache Output { get; set; } = null!;
        public Dictionary<string, string> Projects { get; set; } = null!;
        public List<string> Results { get; set; } = new List<string>();
    }

    public class BuildCache
    {
        public CacheManifest? Manifest;
        public ProjectInstance? Project;
        private readonly PathMapper _pathMapper;
        private readonly Files _files;
        public LabelResult Result;
        private readonly BuildManager _buildManager;
        Func<int> _newConfigurationId;
        public ConfigCache? ConfigCache;
        public ResultsCache? ResultsCache;

        public BuildCache(BazelContext.BazelLabel label, PathMapper pathMapper, Files files, BuildManager? buildManager)
        {
            Result = new LabelResult()
            {
                Label = new Label(label.Workspace, label.Package, label.Name),
                NewIds =new Dictionary<int, int>(),
                OriginalIds = new Dictionary<int, int>()
            };
            _pathMapper = pathMapper;
            _files = files;
            _buildManager = buildManager;
            int id = 0;
            _newConfigurationId = buildManager != null ? buildManager.GetNewConfigurationId : () => ++id;
        }

        public void Initialize(string manifestPath)
        {
            if (!_files.Exists(manifestPath))
            {
                Debug("No input caches found");
                Manifest = new CacheManifest();
                return;
            }

            var cacheManifestJson = File.ReadAllText(manifestPath);
            var cacheManifest = JsonSerializer.Deserialize<CacheManifest>(cacheManifestJson,
                new JsonSerializerOptions() {PropertyNameCaseInsensitive = true});
            Manifest = cacheManifest!;

            var (config, result) = DeserializeCaches();
            _buildManager.ReuseOldCaches(config, result);
        }

        private (ConfigCache aggregatedConfig, ResultsCache aggregatedResults) DeserializeCaches()
        {
            var caches = new Dictionary<string, LabelResult>();
            var cachesInOrder = new List<LabelResult>();
            foreach (var cacheFile in Manifest!.Results)
            {
                using var fileStream = File.OpenRead(cacheFile);
                var translator = BinaryTranslator.GetReadTranslator(fileStream, null);
                LabelResult result = null!;
                translator.Translate(ref result);
                caches[result.Label.ToString()] = result;
                cachesInOrder.Add(result);
            }

            return AggregateCaches(cachesInOrder, caches);
        }

        public (ConfigCache aggregatedConfig, ResultsCache aggregatedResults) AggregateCaches(List<LabelResult> cachesInOrder,
            Dictionary<string, LabelResult> caches)
        {
            var aggregatedResults = new ResultsCache();
            var aggregatedConfig = new ConfigCache();

            foreach (var labelResult in cachesInOrder)
            {
                var configs = labelResult.ConfigCache.GetEnumerator().ToArray();
                var results = labelResult.ResultsCache.GetEnumerator().ToArray();

                foreach (var config in configs)
                {
                    ErrorUtilities.VerifyThrow(aggregatedConfig.GetMatchingConfiguration(config) == null,
                        "Input caches should not contain entries for the same configuration");
                    
                    labelResult.NewIds = new Dictionary<int, int>();
                    var newId = _newConfigurationId();
                    labelResult.NewIds[config.ConfigurationId] = newId;
                    Result.OriginalIds[newId] = config.ConfigurationId;
                    // record which label this configuration belongs to in case our results reference the config
                    Result.ConfigMap[newId] = labelResult.Label.ToString();

                    var newConfig = config.ShallowCloneWithNewId(newId);
                    newConfig.ResultsNodeId = Scheduler.InvalidNodeId;

                    aggregatedConfig.AddConfiguration(newConfig);
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

                    aggregatedResults.AddResult(
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

            ConfigCache = aggregatedConfig;
            ResultsCache = aggregatedResults;
            return (aggregatedConfig, aggregatedResults);
        }

        public void Save(string path)
        {
            var configCache =
                ((IBuildComponentHost) _buildManager!).GetComponent(BuildComponentType.ConfigCache) as IConfigCache;
            var resultsCache =
                ((IBuildComponentHost) _buildManager).GetComponent(BuildComponentType.ResultsCache) as IResultsCache;
            UpdateConfigs(configCache!, resultsCache!);
            DoTranslate(path, CreateWriteTranslator, (t) => TranslateResult(ref Result, t));
        }

        public void UpdateConfigs(IConfigCache configCache, IResultsCache resultsCache)
        {
            if (configCache is ConfigCacheWithOverride withOverride)
                Result.ConfigCache = withOverride.CurrentCache;
            else
                Result.ConfigCache = (ConfigCache) configCache!;

            if (resultsCache is ResultsCacheWithOverride resultsCacheWithOverride)
            {
                Result.ResultsCache = resultsCacheWithOverride.CurrentCache;
                // if we built an external project that had its config in cache, the build result will reference the 
                // new config id rather than the config id that we loaded from cache. Map this back to the configId
                // from cache so future builds can use it
                // also, record the label cache with the corresponding configuration

                // reverse the mapping of the dictionary
                // var toOriginalConfigId = Result.NewMapping.ToDictionary(p => p.Value, p => p.Key);
                // foreach (var result in resultsCacheWithOverride.CurrentCache.GetEnumerator().ToArray())
                // {
                //     if (!Result.ConfigMap.ContainsKey(result.ConfigurationId))
                //     {
                //         Result.ConfigMap[result.ConfigurationId] = Result.Label.ToString();
                //         Result.OriginalIds[result.ConfigurationId] = result.ConfigurationId;
                //     }
                //
                //     // if (toOriginalConfigId.TryGetValue(result.ConfigurationId, out var originalConfigId))
                //     //     result._configurationId = originalConfigId;
                // }
            }
            else
                Result.ResultsCache = (ResultsCache) resultsCache!;
        }

        public void SaveProject(string path)
        {
            Project!.TranslateEntireState = true;
            DoTranslate(path, CreateWriteTranslator, (t) => TranslateProject(ref Project, t));
        }

        public ProjectInstance? LoadProject(string projectPath)
        {
            var manifestPath = _pathMapper.ToManifestPath(projectPath);

            // if (Manifest!.Results.TryGetValue(manifestPath, out var resultCache))
            // {
            //     // var absolutePath = _pathMapper.ToAbsolute(resultCache);
            //     // // don't await here, just queue it for later
            //     // _results[manifestPath] = Task.Run(() => LoadResults(absolutePath) );
            // }

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
            DoTranslate(cachePath, CreateReadTranslator,
                (translator) => { TranslateProject(ref project, translator); });

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
            // SharedReadBuffer buffer = new InterningBinaryReader.Buffer();
            // var reader = InterningBinaryReader.Create(stream, buffer);
            // reader.OpportunisticIntern = new PathMappingInterner(_pathMapper);
            // var translator = new BinaryTranslator.BinaryReadTranslator(stream, buffer, reader);
            // return translator;
        }

        private ITranslator CreateWriteTranslator(Stream stream)
        {
            return BinaryTranslator.GetWriteTranslator(stream);
            // var writer = new PathMappingBinaryWriter(stream, _pathMapper);
            // var translator = new BinaryTranslator.BinaryWriteTranslator(stream, writer);
            // return translator;
        }

        #endregion
    }
}