#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using FluentAssertions;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Moq;
using RulesMSBuild.Tools.Builder;
using Xunit;

namespace RulesMSBuild.Tests.Tools
{
    public interface IObscureDisposable : IDisposable
    {
        void ReallyDispose();
    }
    public class UnclosableMemoryStream : MemoryStream, IObscureDisposable
    {
        public override void Close()
        {
        }

        protected override void Dispose(bool disposing)
        {
        }

        public override ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public void ReallyDispose() => base.Dispose();
    }
    
    public class BuildCacheTests : IDisposable
    {
        private BuildCache _cache = null!;
        private readonly Mock<Files> _files;
        private Dictionary<object, object?> _originalEnv;
        private readonly Mock<PathMapper> _pathMapper;
        private readonly List<IDisposable> _disposables;
        private readonly UnclosableMemoryStream _written;

        public BuildCacheTests()
        {
            _pathMapper = new Mock<PathMapper>();
            _pathMapper.Setup(p => p.ToBazel(It.IsAny<string>()))
                .Returns<string>(str => str.Length > 0 && str.StartsWith('/') ? "YAY" : str);
            _pathMapper.Setup(p => p.FromBazel(It.IsAny<string>()))
                .Returns<string>(str => str.Length > 0 && str == "YAY" ? "/foo/bar" : str);
            _pathMapper.Setup(p => p.ToManifestPath(It.IsAny<string>()))
                .Returns("foo");
            _pathMapper.Setup(p => p.ToAbsolute(It.IsAny<string>()))
                .Returns("foo");
            
            _files = new Mock<Files>();
            ResetCache();
            _originalEnv = new Dictionary<object, object?>();
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
                if (entry.Value is string str && str.Contains("/"))
                {
                    _originalEnv[entry.Key] = entry.Value;
                    Environment.SetEnvironmentVariable((entry.Key as string)!, null);
                }
            
            ProjectCollection.GlobalProjectCollection.EnvironmentProperties.Clear();
            _written = new UnclosableMemoryStream();
            _files.Setup(f => f.Create(It.IsAny<string>())).Returns(_written);
            _files.Setup(f => f.OpenRead(It.IsAny<string>())).Returns(_written);
            _disposables = new List<IDisposable>(){_written};
            
        }

        private void ResetCache()
        {
            _cache = new BuildCache(new CacheManifest()
            {
                Results = new Dictionary<string, string>(){["foo"] = "foo"}
            }, _pathMapper.Object, _files.Object);
        }

        [Fact]
        public void SaveFile_ReplacesPaths()
        {
            var project = new Project(XmlReader.Create(new StringReader(@"
<Project>
    <PropertyGroup>
        <FilePath>/foo/bar</FilePath>
    </PropertyGroup>
</Project>
")));

            _cache.Project = project.CreateProjectInstance();

            _cache.SaveProject("foo");

            VerifyWrittenStrings();
        }

        private void VerifyWrittenStrings()
        {
            _written.Length.Should().BeGreaterThan(0);
            _written.Seek(0, SeekOrigin.Begin);
            var str = new StreamReader(_written).ReadToEnd();
            str.Should().Contain("YAY");
            str.Should().NotContain("foo/bar");
            str.Should().NotContain("/");
            _written.Seek(0, SeekOrigin.Begin);
        }
        
        [Fact]
        public void ReadFile_RestoresPaths()
        {
            SaveFile_ReplacesPaths();
            
            ResetCache();
            
            _cache.LoadProject("foo");

            var path = _cache.Project!.Properties.FirstOrDefault(p => p.Name == "FilePath");
            path.Should().NotBeNull();
            path!.EvaluatedValue.Should().Be("/foo/bar");

        }

        [Fact]
        public void SaveResult_ReplacesPaths()
        {
            var result = new BuildResult();
            result.AddResultsForTarget("foo",
                new TargetResult(new[]
                {
                    new ProjectItemInstance.TaskItem("/foo/bar", "/foo/bar")
                }, new WorkUnitResult()));
            _cache.RecordResult(result);   
            _cache.Save("foo");
            VerifyWrittenStrings();
        }
        
        [Fact]
        public async Task LoadResult_RestoresPaths()
        {
            SaveResult_ReplacesPaths();
            ResetCache();
            _cache.LoadProject("foo");
            var resultTask =_cache.TryGetResults("foo");
            resultTask.Should().NotBeNull();
            var result = await resultTask!;

            result.ResultsByTarget.Count.Should().Be(1);
            var targetResult = result.ResultsByTarget.Values.Single();
            targetResult.Items.Length.Should().Be(1);
            var item = targetResult.Items[0];
            item.ItemSpec.Should().Be("/foo/bar");
        }

        public void Dispose()
        {
            foreach (var (key, value) in _originalEnv)
            {
                 Environment.SetEnvironmentVariable((key as string)!, value as string);
            }

            foreach (var disposable in _disposables)
            {
                if (disposable is IObscureDisposable obscure)
                    obscure.ReallyDispose();
                else 
                    disposable.Dispose();
            }
        }
    }
}
