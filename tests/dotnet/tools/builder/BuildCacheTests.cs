#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using FluentAssertions;
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

        public BuildCacheTests()
        {
            _pathMapper = new Mock<PathMapper>();
            _pathMapper.Setup(p => p.ToBazel(It.IsAny<string>()))
                .Returns<string>(str => str.Length > 0 && str.StartsWith('/') ? "YAY" : str);
            _pathMapper.Setup(p => p.FromBazel(It.IsAny<string>()))
                .Returns<string>(str => str.Length > 0 && str == "YAY" ? "/foo/bar" : str);
            
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
            _disposables = new List<IDisposable>();
        }

        private void ResetCache()
        {
            _cache = new BuildCache(new CacheManifest(), _pathMapper.Object, _files.Object);
        }


        [Fact]
        public UnclosableMemoryStream SaveFile_ReplacesPaths()
        {
            var written = new UnclosableMemoryStream();
            _disposables.Add(written);
            _files.Setup(f => f.Create(It.IsAny<string>())).Returns(written);
            
            var project = new Project(XmlReader.Create(new StringReader(@"
<Project>
    <PropertyGroup>
        <FilePath>/foo/bar</FilePath>
    </PropertyGroup>
</Project>
")));

            _cache.Project = project.CreateProjectInstance();
            // _cache.RecordResult(new BuildResult()
            // {
            //     ProjectStateAfterBuild = project.CreateProjectInstance()
            // });
            
            _cache.Save("foo");

            written.Length.Should().BeGreaterThan(0);
            written.Seek(0, SeekOrigin.Begin);
            var str = new StreamReader(written).ReadToEnd();
            str.Should().Contain("YAY");
            str.Should().NotContain("foo/bar");
            str.Should().NotContain("/");
            return written;
        }

        [Fact]
        public void ReadFile_RestoresPaths()
        {
            var stream = SaveFile_ReplacesPaths();
            _files.Setup(f => f.OpenRead(It.IsAny<string>())).Returns(stream);
            stream.Seek(0, SeekOrigin.Begin);
            ResetCache();
            
            // _cache.Load("foo");

            var path = _cache.Project.Properties.FirstOrDefault(p => p.Name == "FilePath");
            path.Should().NotBeNull();
            path!.EvaluatedValue.Should().Be("/foo/bar");

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
