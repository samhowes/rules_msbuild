using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Build;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using RulesMSBuild.Tools.Builder.Diagnostics;
using RulesMSBuild.Tools.Builder.MSBuild;
using static RulesMSBuild.Tools.Builder.BazelLogger;

#pragma warning disable 8618

namespace RulesMSBuild.Tools.Builder
{
    public class LabelResult : ITranslatable
    {
        public BazelContext.BazelLabel Label;
        public ProjectInstance Project;
        public BuildResult BuildResult;
        
        public void Translate(ITranslator translator)
        {
            // translator.Translate(ref Label);
            translator.Translate(ref BuildResult);
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
        public Dictionary<string, string> Results { get; set; } = null!;
    }
    
    public class BuildCache
    {
        public CacheManifest? Manifest;
        public ProjectInstance? Project;
        private readonly PathMapper _pathMapper;
        private readonly Files _files;
        private BuildResult _result;
        private Dictionary<string, Task<BuildResult>> _results = new Dictionary<string, Task<BuildResult>>();

        public BuildCache(CacheManifest manifest, PathMapper pathMapper, Files files)
        {
            Manifest = manifest;
            _pathMapper = pathMapper;
            _files = files;
        }
        
        public void Initialize(string manifestPath)
        {
            // Debugger.WaitForAttach();
            if (!_files.Exists(manifestPath))
            {
                Debug("No input caches found");
                return;
            }
            var cacheManifestJson = File.ReadAllText(manifestPath);
            var cacheManifest = JsonSerializer.Deserialize<CacheManifest>(cacheManifestJson,
                new JsonSerializerOptions() {PropertyNameCaseInsensitive = true});
            Manifest = cacheManifest!;
        }

        public void RecordResult(BuildResult buildResult)
        {
            _result = buildResult;
        }

        public void Save(string path)
        {
            DoTranslate(path, CreateWriteTranslator, (t) => TranslateResult(ref _result, t));
        }
        
        public void SaveProject(string path)
        {
            Project!.TranslateEntireState = true;
            DoTranslate(path, CreateWriteTranslator, (t) => TranslateProject(ref Project, t));
        }

        public ProjectInstance? LoadProject(string projectPath)
        {
            var manifestPath = _pathMapper.ToManifestPath(projectPath);

            if (Manifest!.Results.TryGetValue(manifestPath, out var resultCache))
            {
                // don't await here, just queue it for later
                _results[manifestPath] = LoadResultsAsync(resultCache);
            }
            
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

        private Task<BuildResult> LoadResultsAsync(string resultCachePath)
        {
            return Task.Run(() =>
            {
                BuildResult buildResult = null!;
                DoTranslate(resultCachePath, CreateReadTranslator,
                    (translator) => { TranslateResult(ref buildResult, translator); });
                return buildResult;
            });
        }

        public ProjectInstance? LoadProjectImpl(string cachePath)
        {
            ProjectInstance project = null!;
            // Debugger.WaitForAttach();
            DoTranslate(cachePath, CreateReadTranslator,
                (translator) => { TranslateProject(ref project, translator); });

            return project;
        }

        #region Translation
        private void TranslateProject(ref ProjectInstance project, ITranslator translator)
        {
            translator.Translate(ref project, ProjectInstance.FactoryForDeserialization);
        }
        private void TranslateResult(ref BuildResult buildResult, ITranslator translator)
        {
            translator.Translate(ref buildResult, BuildResult.FactoryForDeserialization);
        }
        private void DoTranslate(string path, Func<Stream, ITranslator> createTranslator, Action<ITranslator> translate)
        {
            using var stream = createTranslator == CreateReadTranslator ? _files.OpenRead(path) : _files.Create(path);
            var translator = createTranslator(stream);
            translate(translator);
        }
        
        private BinaryTranslator.BinaryReadTranslator CreateReadTranslator(Stream stream)
        {
            SharedReadBuffer buffer = new InterningBinaryReader.Buffer();
            var reader = InterningBinaryReader.Create(stream, buffer);
            reader.OpportunisticIntern = new PathMappingInterner(_pathMapper);
            var translator = new BinaryTranslator.BinaryReadTranslator(stream, buffer, reader);
            return translator;
        }
        
        private BinaryTranslator.BinaryWriteTranslator CreateWriteTranslator(Stream stream)
        {
            var writer = new PathMappingBinaryWriter(stream, _pathMapper);
            var translator = new BinaryTranslator.BinaryWriteTranslator(stream, writer);
            return translator;
        }
        #endregion
        
    }
}