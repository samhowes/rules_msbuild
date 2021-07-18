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
using Microsoft.Build.Internal;
using Microsoft.Build.Utilities;
using Moq;
using RulesMSBuild.Tools.Builder;
using Xunit;
using Task = System.Threading.Tasks.Task;

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
        private List<LabelResult> _cachesInOrder = new List<LabelResult>();
        private Dictionary<string, LabelResult> _caches = new Dictionary<string, LabelResult>();

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
            // _cache = new BuildCache(new CacheManifest()
            // {
            //     Results = new List<string>(){ "foo"}
            // }, _pathMapper.Object, _files.Object);
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
            
            _cache.Save("foo");
            VerifyWrittenStrings();
        }
        
        [Fact]
        public async Task LoadResult_RestoresPaths()
        {
            SaveResult_ReplacesPaths();
            ResetCache();
            _cache.LoadProject("foo");
            // var resultTask =_cache.TryGetResults("foo");
            // resultTask.Should().NotBeNull();
            // var result = await resultTask!;

            // result.ResultsByTarget.Count.Should().Be(1);
            // var targetResult = result.ResultsByTarget.Values.Single();
            // targetResult.Items.Length.Should().Be(1);
            // var item = targetResult.Items[0];
            // item.ItemSpec.Should().Be("/foo/bar");
        }

        [Fact]
        public void Aggregate_Works()
        {
            InitNoBuildManager();

            var r = Result("first");
            AddResults(r, 1, "Build");

            var (config, result) = Aggregate();
            config.Length.Should().Be(1);
            result.Length.Should().Be(1);
            config[0].ConfigurationId.Should().Be(1);
            result[0].ConfigurationId.Should().Be(1);
        }
        
        [Fact]
        public void Aggregate_MultipleSources()
        {
            InitNoBuildManager();

            var first = Result("first");
            AddResults(first, 1, "Build");
            var second = Result("second");
            AddResults(second, 1, "Build");

            var (config, result) = Aggregate();
            config.Length.Should().Be(2);
            result.Length.Should().Be(2);
            config[0].ConfigurationId.Should().Be(1);
            result[0].ConfigurationId.Should().Be(1);
            config[1].ConfigurationId.Should().Be(2);
            result[1].ConfigurationId.Should().Be(2);

            _cache.Result.ConfigMap[1].Should().Be(first.Label.ToString());
            _cache.Result.ConfigMap[2].Should().Be(second.Label.ToString());
        }
        
        [Fact]
        public void Aggregate_MixedResults()
        {
            InitNoBuildManager();

            var first = Result("first");
            AddResults(first, 1, "Build");
            var second = Result("second");
            AddResults(second, 1, "Build");
            AddResults(second, 2, "BuildReference", first.Label, 1);

            var (config, result) = Aggregate();
            config.Length.Should().Be(2);
            
            // length should be two because the cache should merge the results
            result.Length.Should().Be(2); 
            config[0].ConfigurationId.Should().Be(1);
            result[0].ConfigurationId.Should().Be(1);
            config[1].ConfigurationId.Should().Be(2);
            result[1].ConfigurationId.Should().Be(2);

            result[0].ResultsByTarget.Keys.OrderBy(k => k).Should().Equal("Build", "BuildReference");
            result[1].ResultsByTarget.Keys.OrderBy(k => k).Should().Equal("Build");

            
            _cache.Result.ConfigMap[1].Should().Be(first.Label.ToString());
            _cache.Result.OriginalIds[1].Should().Be(1);
            first.NewIds[1].Should().Be(1);
            _cache.Result.OriginalIds[2].Should().Be(1);
            second.NewIds[1].Should().Be(2);
        }

        private void InitNoBuildManager()
        {
            _cache = new BuildCache(
                new BazelContext.BazelLabel("wkspc", "pkg", "current"),
                _pathMapper.Object,
                _files.Object,
                null! // we won't be using the build manager
            );
        }

        private (BuildRequestConfiguration[], BuildResult[]) Aggregate()
        {
            var (config, result) = _cache.AggregateCaches(_cachesInOrder, _caches);
            return (config.GetEnumerator().ToArray().OrderBy(c => c.ConfigurationId).ToArray(),
                    result.GetEnumerator().ToArray().OrderBy(r => r.ConfigurationId).ToArray()
                );
        }
        
        private LabelResult Result(string name)
        {
            var r = new LabelResult
            {
                OriginalIds = new Dictionary<int, int>(),
                ConfigCache = new ConfigCache(),
                ResultsCache = new ResultsCache(),
                Label = new Label("wkspc", "pkg", name)
            };
            _caches[r.Label.ToString()] = r;
            _cachesInOrder.Add(r);
            return r;
        }

        private void AddResults(LabelResult r, int configId, string targetName, BazelContext.BazelLabel? other = null, int? otherId = null)
        {
            if (other == null)
            {
                r.ConfigCache.AddConfiguration(
                    new BuildRequestConfiguration(configId,
                        new BuildRequestData(r.Label.ToString(), 
                            new Dictionary<string, string>(), 
                            "Current", new []{"build"}, null),
                        "Current"));
            }
            

            if (other != null)
            {
                r.ConfigMap[configId] = other.ToString();
                r.OriginalIds[configId] = otherId!.Value;
            }
            else
            {
                r.OriginalIds[configId] = configId;    
            }
            
            
            var silly = new BuildResult();
            silly.AddResultsForTarget(targetName, new TargetResult(Array.Empty<ProjectItemInstance.TaskItem>(), new WorkUnitResult()));
            var actual = new BuildResult(silly, -1, configId, -1, -1, -1);
            r.ResultsCache.AddResult(actual);
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
