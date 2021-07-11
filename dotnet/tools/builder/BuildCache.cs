using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
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
        public Dictionary<string, string> ProjectResults { get; set; } = null!;
    }
    
    public class BuildCache : ITranslatable
    {
        public CacheManifest? Manifest;
        public ProjectInstance? Project;
        private readonly PathMapper _pathMapper;
        private readonly Files _files;

        public BuildCache(CacheManifest manifest, PathMapper pathMapper, Files files)
        {
            Manifest = manifest;
            _pathMapper = pathMapper;
            _files = files;
        }

        public void RecordResult(GraphBuildResult buildResult)
        {
        }

        public void Save(string path)
        {
            using var stream = _files.Create(path);
            var translator = CreateWriteTranslator(stream);
            Translate(translator);
        }

        private BinaryTranslator.BinaryWriteTranslator CreateWriteTranslator(Stream stream)
        {
            var writer = new PathMappingBinaryWriter(stream, _pathMapper);
            var translator = new BinaryTranslator.BinaryWriteTranslator(stream, writer);
            return translator;
        }

        public void Load(string path)
        {
            using var stream = _files.OpenRead(path);
            var translator = CreateReadTranslator(stream);
            Translate(translator);
        }

        private BinaryTranslator.BinaryReadTranslator CreateReadTranslator(Stream stream)
        {
            SharedReadBuffer buffer = new InterningBinaryReader.Buffer();
            var reader = InterningBinaryReader.Create(stream, buffer);
            reader.OpportunisticIntern = new PathMappingInterner(_pathMapper);
            var translator = new BinaryTranslator.BinaryReadTranslator(stream, buffer, reader);
            return translator;
        }

        public void Translate(ITranslator translator)
        {
            
        }

        public void SaveProject(string path)
        {
            Project.TranslateEntireState = true;
            DoTranslate(path, CreateWriteTranslator, (translator) =>
            {
                TranslateProject(ref Project, translator);
            });
        }

        public ProjectInstance? LoadProject(string projectPath)
        {
            var manifestPath = _pathMapper.ToManifestPath(projectPath);
            string? cachePath = null;
            if (Manifest.Projects?.TryGetValue(manifestPath, out cachePath) != true)
            {
                Debug($"Project cache miss: {manifestPath}");
                return null;
            }
            
            cachePath = _pathMapper.ToAbsolute(cachePath!);
            return LoadProjectImpl(cachePath);
        }

        public ProjectInstance? LoadProjectImpl(string cachePath)
        {
            ProjectInstance project = null!;
            // Debugger.WaitForAttach();
            DoTranslate(cachePath, CreateReadTranslator, (translator) => { TranslateProject(ref project, translator); });

            return project;
        }

        private void DoTranslate(string path, Func<Stream, ITranslator> createTranslator, Action<ITranslator> translate)
        {
            using var stream = createTranslator == CreateReadTranslator ? _files.OpenRead(path) : _files.Create(path);
            var translator = createTranslator(stream);
            translate(translator);
        }

        private void TranslateProject(ref ProjectInstance project, ITranslator translator)
        {
            translator.Translate(ref project, ProjectInstance.FactoryForDeserialization);
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
    }
}