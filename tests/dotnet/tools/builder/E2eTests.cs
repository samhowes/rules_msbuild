#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using FluentAssertions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Moq;
using Newtonsoft.Json;
using RulesMSBuild.Tools.Bazel;
using RulesMSBuild.Tools.Builder;
using RulesMSBuild.Tools.Builder.Caching;
using RulesMSBuild.Tools.Builder.MSBuild;
using Xunit;
using Xunit.Abstractions;
using Label = RulesMSBuild.Tools.Builder.Caching.Label;

namespace RulesMSBuild.Tests.Tools
{
    [Collection(BuildFrameworkTestCollection.TestCollectionName)]
    public class E2eTests : IDisposable
    {
        private readonly ITestOutputHelper _helper;
        private Builder _builder = null!;
        private readonly string _tmp;
        private BuildContext _context = null!;
        private List<string> _targetLog = null!;
        private bool _logInited;
        private StringBuilder? _projectFile;
        private BuilderDependencies _deps = null!;
        private CacheManifest? _nextManifest;
        private string _projectPath;
        private readonly string _execRoot;
        private string _nextManifestPath;

        public E2eTests(ITestOutputHelper helper)
        {
            _helper = helper;
            
            _tmp = BazelEnvironment.GetTmpDir(nameof(E2eTests));
            string execRoot = Path.Combine(_tmp, "execroot");
            Directory.CreateDirectory(execRoot);
            Directory.SetCurrentDirectory(execRoot);
            _execRoot = Directory.GetCurrentDirectory(); // in case _execRoot is a symlink (/var => /private/var)
            _tmp = Path.GetDirectoryName(_execRoot)!;
        }

        private void PrepareNewBuild(string projectName, string action = "build")
        {
            // keep as a separate method so we cn make sure to register the msbuild assemblies first.
            PathMapper.ResetInstance();
            Directory.SetCurrentDirectory(_execRoot);
            var lastProject = _context?.ProjectFile;
            var configId = typeof(BuildManager).GetField("s_nextBuildRequestConfigurationId", BindingFlags.Static | BindingFlags.NonPublic);
            configId!.SetValue(null, 0);
            if (Path.IsPathRooted(projectName))
                projectName = projectName[(_execRoot.Length + 1)..];
            _context = new BuildContext(new BuildCommand()
            {
                Action = action,
                bazel_output_base = _tmp,
                bazel_bin_dir = "cpu-dbg",
                workspace = "e2e",
                package = (Path.GetDirectoryName(projectName) ?? "").Replace("\\","/"),
                label_name = Path.GetFileNameWithoutExtension(projectName) + "_" + action,
                nuget_config = "NuGet.config",
                tfm = "netcoreapp3.1",
                directory_bazel_props = "Bazel.props",
                configuration = "dbg",
                output_type = "lib",
                sdk_root = "foo",
                project_file = projectName,
                DirectorySrcs = Array.Empty<string>()
            }) {DiagnosticsEnabled = true};
            
            _context.MakeTargetGraph(true);
            _deps = new BuilderDependencies(_context, buildLog: new Mock<IBazelMsBuildLogger>().Object);
            
            _targetLog = new List<string>();
            _logInited = false;
            
            var testLogger = new Mock<ILogger>();
            testLogger.Setup(l => l.Initialize(It.IsAny<IEventSource>())).Callback<IEventSource>(InitTestLog);
            _deps.Loggers.Add(testLogger.Object);
            _builder = new Builder(_context, _deps);

            WriteCacheManifest(lastProject);
        }

        private void InitTestLog(IEventSource eventSource)
        {
            if (_logInited) return;
            
            _logInited = true;

            var projectStack = new Stack<string>();
            eventSource.ProjectStarted += (_, projectStarted) =>
            {
                var name = Path.GetFileNameWithoutExtension(projectStarted.ProjectFile);
                projectStack.Push(name);
            };
            eventSource.TargetStarted += (_, targetStarted) =>
            {
                _targetLog.Add($"{projectStack.Peek()}:{targetStarted.TargetName}");
            };
            eventSource.ErrorRaised += (_, err) =>
            {
                _helper.WriteLine("[error]" + err.Message);
            };
            eventSource.ProjectFinished += (_, _) =>
            {
                projectStack.Pop();
            };
        }

        [Fact]
        public void ASimpleBuildWorks()
        {
            PrepareNewBuild("foo.csproj");
            StartProject();
            AddTarget("Build");
            SaveProject();                
            
            BuildAndVerifyTargets("foo:Build");
        }
        
        [Fact]
        public void BuildFromCache_SkipsSecondTime()
        {
            PrepareNewBuild("foo.csproj");
            StartProject();
            AddTarget("CacheMe");
            AddTarget("Build", "CacheMe");
            SaveProject();
            
            BuildAndVerifyTargets(
                "foo:CacheMe",
                "foo:Build"
                );
            
            PrepareNewBuild("foo.csproj");

            VerifyBuiltTargets(/*none*/);
        }
   
        [Fact]
        public void Build_ReusesIntermediateResults()
        {
            
            PrepareNewBuild("foo.csproj", "build");
            StartProject();
            
            // first build
            AddTarget("CacheMe");
            AddTarget("Build", "CacheMe");
            
            // second build: original ResultsCacheWithOverride won't record "_PublishImpl"
            // as we're not building it directly 
            AddTarget("_PublishImpl", "Build", "CacheMe");
            AddTarget("Publish", "_PublishImpl");
            
            // third build
            AddTarget("Pack", "_PublishImpl");
            
            SaveProject();
            // normal, no caching
            BuildAndVerifyTargets("foo:CacheMe", "foo:Build");

            PrepareNewBuild("foo.csproj", "publish");
            // nothing special, normal caching produces this
            BuildAndVerifyTargets("foo:_PublishImpl", "foo:Publish"); 
            
            PrepareNewBuild("foo.csproj", "pack");
            
            // our custom code kicks in here:
            // since this target depends on _PublishImpl, but we only explicitly built Publish, the default cache 
            // won't include _PublishImpl in the results cache. Our custom code does.
            BuildAndVerifyTargets("foo:Pack");
        }

        [Fact]
        public void Build_OnlySavesNewResults_ToCache()
        {
            PrepareNewBuild("foo.csproj", "build");
            StartProject();
            
            // first build
            AddTarget("CacheMe");
            AddTarget("Build", "CacheMe");
            
            // second build
            AddTarget("Publish", "Build");
            
            SaveProject();
            
            // normal, no caching
            BuildAndVerifyTargets("foo:CacheMe", "foo:Build");

            PrepareNewBuild("foo.csproj", "publish");
            
            BuildAndVerifyTargets("foo:Publish");
            
            VerifyCachedTargets("foo:Publish");
        }

        [Fact]
        public void BuildProjectReference_IsCached()
        {
            PrepareNewBuild("foo.csproj");
            StartProject();
            AddTarget("CacheMe");
            AddTarget("Build", "CacheMe");
            SaveProject();

            BuildAndVerifyTargets("foo:CacheMe", "foo:Build");
            
            var fooPath = _context.ProjectFile;
            
            PrepareNewBuild("bar.csproj");
            
            StartProject();
            AddBuildReferenceTarget("BuildReference", fooPath, "CacheMe");
            AddTarget("Build", "BuildReference");
            SaveProject();
            
            BuildAndVerifyTargets(
                /*foo:CacheMe is not built */
                "bar:BuildReference",
                "bar:Build"
                );
        }
        
        [Fact]
        public void TargetFrameworkAsGlobalProperty_IsCached()
        {
            // see BuildManger.ExecuteBuild switch... case "pack": for justification
            PrepareNewBuild("foo.csproj", "build");
            StartProject();
            _projectFile!.AppendLine($@"<Target Name='Pack'>
    <MSBuild Projects='$(MSBuildProjectFullPath)' 
            Targets='CacheMe'
            Properties='TargetFramework=netcoreapp3.1'
    ></MSBuild>
</Target>
");
            AddTarget("CacheMe");
            AddTarget("Build", "CacheMe");
            SaveProject();
            
            BuildAndVerifyTargets(
                "foo:CacheMe",
                "foo:Build"
            );
            
            PrepareNewBuild("foo.csproj", "pack");
            
            BuildAndVerifyTargets(
                "foo:Pack"
                 /* CacheMe should not be built */
                );
        }
        
        [Fact]
        public void CachedReference_NewTarget_Works()
        {
            // this is what happens with the GetTargetFrameworks reference in a normal Build.
            // GetTargetFrameworks is not built by the primary build process, but is instead built by references.
            PrepareNewBuild("foo.csproj");
            StartProject();
            AddTarget("CacheMe"); 
            AddTarget("Build", "CacheMe");
            // This target is not built by Build, but only built by referencing projects
            AddTarget("ReferenceMe");
            SaveProject();
            
            BuildAndVerifyTargets("foo:CacheMe", "foo:Build");
            
            var fooPath = _context.ProjectFile;
            PrepareNewBuild("bar.csproj");
            
            StartProject();
            // this will add a result for the foo.csproj configuration to the current results cache
            // The stock version of MSBuild throws a "caches should not overlap" internal exception when we build this 
            // way (when its compiled in debug mode) 
            AddBuildReferenceTarget("BuildReference", fooPath, "ReferenceMe");
            AddTarget("Build", "BuildReference");
            SaveProject();
            
            BuildAndVerifyTargets(
                "bar:BuildReference",
                "foo:ReferenceMe",
                "bar:Build"
                );
            
            // make sure we stored the results from foo in our current cache
            VerifyCachedTargets(
                "bar:Build",
                "bar:BuildReference",
                // this final target is not supported by stock MSBuild because it mixes results for configurations
                "foo:ReferenceMe"); 

        }

        [Fact]
        public void CachedProjectReferences_EvaluateToCorrectPath()
        {
            PrepareNewBuild("foo/foo.csproj");
            StartProject("foo/foo.csproj");
            AddTarget("Build");
            SaveProject();
            BuildAndVerifyTargets("foo:Build");
            
            PrepareNewBuild("foo/bar/bar.csproj");
            StartProject();
            AddTarget("Build");
            // will be not be a valid relative path from bam.csproj, but will be a valid relative path from bar.csproj
            AddReference("../foo.csproj");
            SaveProject();
            BuildAndVerifyTargets("bar:Build");
            
            PrepareNewBuild("foo/bar/bam/bam.csproj");
            StartProject();
            AddTarget("Build");
            // will be a valid relative path from bam.csproj
            AddReference("../bar.csproj");
            SaveProject();
            
            // should not throw while loading foo/foo.csproj
            // should *not* try to load foo/bar/foo.csproj
            BuildAndVerifyTargets("bam:Build");
        }

        
        private void VerifyCachedTargets(params string[] expectations)
        {
            WriteCacheManifest(_context.ProjectFile);
            var cache = new BuildCache(new Label("_", "_", "_"), null!, new Files(), null) {Manifest = _nextManifest};
            var (caches, _) = cache.DeserializeCaches();
            
            // we want the most recent results
            var last = caches[_context.Bazel.Label.ToString()];
            
            // but we need all the configs to get the ProjectFullPath
            cache.Initialize(_nextManifestPath!, null);
            var cached = (
                from result in last.Results 
                let config = cache.ConfigCache[result.ConfigurationId] 
                from targetName in result.ResultsByTarget.Keys 
                let shortName = Path.GetFileNameWithoutExtension(config.ProjectFullPath) 
                select $"{shortName}:{targetName}").ToList();

            cached = cached.OrderBy(c => c).ToList();
            cached.Should().Equal(expectations);
        }

        private void BuildAndVerifyTargets(params string[] expectedTargets)
        {
            var result = _builder.Build();
            result.Should().Be(0);
            VerifyBuiltTargets(expectedTargets);
        }

        
        private void AddBuildReferenceTarget(string targetName, string projectPath, string referencedTargets)
        {
            _projectFile!.AppendLine($@"<Target Name='{targetName}'>
    <MSBuild Projects='{projectPath}' Targets='{referencedTargets}'></MSBuild>
</Target>");

            AddReference(projectPath);
        }

        private void AddReference(string projectPath)
        {
            _projectFile!.AppendLine($@"<ItemGroup>
    <ProjectReference Include='{projectPath}' />
</ItemGroup>
");
        }

        private void SaveProject()
        {
            _projectFile!.AppendLine("</Project>");
            var contents = _projectFile.ToString();
            var dir = Path.GetDirectoryName(_projectPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir!);
            File.WriteAllText(_projectPath, contents);
        }

        private void StartProject(string? path = null)
        {
            _projectPath = path != null ? Path.Combine(_context.ExecPath(path)) : _context.ProjectFile;
            _projectFile = new StringBuilder(@"<Project>
    <PropertyGroup>
        <TargetFramework>netcoreapp3.1</TargetFramework>
    </PropertyGroup>

");
        }

        private void AddTarget(string targetName, params string[] dependsOnTargets)
        {
            var dependsOn = dependsOnTargets.Any() ? $" DependsOnTargets='{string.Join(";", dependsOnTargets)}'" : "";
            
            _projectFile!.AppendLine($@"    <Target Name='{targetName}'{dependsOn}>
        <Message Text='{targetName}.Built' Importance='High' />
    </Target>
");
        }

        private void WriteCacheManifest(string? lastProject)
        {
            var projectCachePath =
                _context.OutputPath(Path.GetFileName(_context.ProjectFile) + $".{_context.Command.Action}.cache");
            var lastManifest = _nextManifest;
            _nextManifest = new CacheManifest()
            {
                Output = new CacheManifest.BuildResultCache()
                {
                    Project = projectCachePath,
                    Result = _context.LabelPath(".cache")
                },
                Projects = new Dictionary<string, string>()
                {
                }
            };
            
            if (lastManifest != null)
            {
                foreach (var project in lastManifest.Projects)
                {
                    _nextManifest.Projects[project.Key] = project.Value;
                }
                _nextManifest.Projects[_deps.PathMapper.ToManifestPath(lastProject!)] =
                    lastManifest.Output.Project;
                _nextManifest.Results.AddRange(lastManifest.Results);
                _nextManifest.Results.Add(lastManifest.Output.Result);
            }

            _nextManifestPath = _context.LabelPath(".cache_manifest");
            Directory.CreateDirectory(Path.GetDirectoryName(_nextManifestPath)!);
            File.WriteAllText(_nextManifestPath, JsonConvert.SerializeObject(_nextManifest));
        }

        public void VerifyBuiltTargets(params string[] targets)
        {
            _targetLog.Should().Equal(targets);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_tmp, true);
            }
            catch 
            {
                //ignored
            }
        }
    }
}