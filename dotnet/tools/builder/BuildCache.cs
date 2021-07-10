using System.Collections.Generic;
using System.IO;
using Microsoft.Build;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using RulesMSBuild.Tools.Builder.MSBuild;

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
            translator.Translate(ref Project, ProjectInstance.FactoryForDeserialization);
            translator.Translate(ref BuildResult);
        }
    }

    public class CacheManifest
    {
        public Dictionary<string, string> Projects { get; set; } = null!;
        public Dictionary<string, string> ProjectResults { get; set; } = null!;
    }
    
    public class BuildCache : ITranslatable
    {
        private readonly CacheManifest _manifest;
        public ProjectInstance Project;
        private readonly PathMapper _pathMapper;
        private readonly Files _files;

        public BuildCache(CacheManifest manifest, PathMapper pathMapper, Files files)
        {
            _manifest = manifest;
            _pathMapper = pathMapper;
            _files = files;
        }

        public void RecordResult(BuildResult buildResult)
        {
            var project = buildResult.ProjectStateAfterBuild;
            Project = project;
        }

        public void Save(string path)
        {
            using var stream = _files.Create(path);;
            var writer = new PathMappingBinaryWriter(stream, _pathMapper);
            var translator = new BinaryTranslator.BinaryWriteTranslator(stream, writer);
            
            Translate(translator);
        }

        public void Load(string path)
        {
            using var stream = _files.OpenRead(path);

            SharedReadBuffer buffer = new InterningBinaryReader.Buffer();
            var reader = InterningBinaryReader.Create(stream, buffer);
            reader.OpportunisticIntern = new PathMappingInterner(_pathMapper);
            var translator = new BinaryTranslator.BinaryReadTranslator(stream, buffer, reader);
            
            Translate(translator);
        }

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref Project, ProjectInstance.FactoryForDeserialization);
        }
    }
}