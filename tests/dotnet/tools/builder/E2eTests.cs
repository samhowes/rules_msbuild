#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using FluentAssertions;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Moq;
using Newtonsoft.Json;
using RulesMSBuild.Tools.Bazel;
using RulesMSBuild.Tools.Builder;
using RulesMSBuild.Tools.Builder.Diagnostics;
using RulesMSBuild.Tools.Builder.MSBuild;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace RulesMSBuild.Tests.Tools
{
    public class E2eTests : IDisposable
    {
        private readonly ITestOutputHelper _helper;
        private Builder _builder;
        private string _tmp;
        private BuildContext _context;
        private string _execRoot;
        private readonly List<string> _log;
        private List<string> _targetLog;
        private bool _logInited;

        public E2eTests(ITestOutputHelper helper)
        {
            _helper = helper;
            _log = new List<string>();
            Program.RegisterSdk("/usr/local/share/dotnet/sdk/5.0.203");
            _tmp = BazelEnvironment.GetTmpDir(nameof(E2eTests));
            _execRoot = Path.Combine(_tmp, "execroot");
            Directory.CreateDirectory(_execRoot);
            Directory.SetCurrentDirectory(_execRoot);
            _execRoot = Directory.GetCurrentDirectory(); // in case _execRoot is a symlink
            _tmp = Path.GetDirectoryName(_execRoot);
        }

        private void Init(string projectName, string action = "build")
        {
            // keep as a separate method so we cn make sure to register the msbuild assemblies first.
            PathMapper.ResetInstance();
            var configId = typeof(BuildManager).GetField("s_nextBuildRequestConfigurationId", BindingFlags.Static | BindingFlags.NonPublic);
            configId!.SetValue(null, 0);
            _context = new BuildContext(new Command()
            {
                Action = action,
                NamedArgs =
                {
                    ["bazel_output_base"] = _tmp,
                    ["bazel_bin_dir"] = "foo-dbg",
                    ["workspace"] = "e2e",
                    ["package"] = "foo",
                    ["label_name"] = Path.GetFileNameWithoutExtension(projectName),
                    ["nuget_config"] = "NuGet.config",
                    ["tfm"] = "netcoreapp3.1",
                    ["directory_bazel_props"] = "Bazel.props",
                    ["configuration"] = "dbg",
                    ["output_type"] = "lib",
                    ["sdk_root"] = "foo",
                    ["project_file"] = projectName,
                }
            }) {DiagnosticsEnabled = true};
            
            _context.MakeTargetGraph(true);
            _targetLog = new List<string>();
            _logInited = false;
            var testLogger = new Mock<IBazelMsBuildLogger>();
            testLogger.Setup(l => l.Initialize(It.IsAny<IEventSource>())).Callback<IEventSource>(InitTestLog);
            testLogger.Setup(l => l.Initialize(It.IsAny<IEventSource>(), It.IsAny<int>())).Callback<IEventSource,int>((eventSource, _) => InitTestLog(eventSource));
            _builder = new Builder(_context, testLogger.Object);
        }

        private void InitTestLog(IEventSource eventSource)
        {
            if (_logInited) return;
            
            _logInited = true;

            var projectStack = new Stack<string>();
            eventSource.ProjectStarted += (_, projectStarted) =>
            {
                var name = Path.GetFileNameWithoutExtension(projectStarted.ProjectFile);
                // _targetLog.Add(
                //     $"{name}:{projectStarted.TargetNames}"
                // );
                projectStack.Push(name);
            };
            eventSource.TargetStarted += (_, targetStarted) =>
            {
                _targetLog.Add($"{projectStack.Peek()}:{targetStarted.TargetName}");
            };
            
            eventSource.ProjectFinished += (_, _) =>
            {
                projectStack.Pop();
            };
        }

        [Fact]
        public void ASimpleBuildWorks()
        {
            Init("foo.csproj");
            File.WriteAllText(_context.ProjectFile, @"<Project>
    <PropertyGroup>
        <TargetFramework>netcoreapp3.1</TargetFramework>
    </PropertyGroup>
    <Target Name='Build'>
        <Message Text='Foo' Importance='High' />
    </Target>
</Project>");
            
            var result = _builder.Build();
            
            result.Should().Be(0);

            _targetLog.Should().Equal(new[]
            {
                "foo:Build"
            });

            // var built = _context.TargetGraph;
            // built!.Clusters.Count.Should().Be(1);
            // var cluster = built.Clusters.Values.Single();
            //
            // cluster.Nodes.Count.Should().Be(1);
            // var node = cluster.Nodes.Values.Single();
            // node.Name.Should().Be("Build");
        }
        
        [Fact]
        public void BuildFromCache_SkipsSecondTime()
        {
            Init("foo.csproj");
            MakeProjectFile("CacheMe");
            
            var result = _builder.Build();
            
            result.Should().Be(0);
            VerifyBuiltTargets(
                "foo:CacheMe",
                "foo:Build"
                );
            WriteCacheManifest();
            Init("foo.csproj");

            result = _builder.Build();

            result.Should().Be(0);
            VerifyBuiltTargets(/*none*/);
        }

        private (string path, CacheManifest cacheManifest) BuildAndCache(string projectName, string targetToBuild, string extraTarget = null)
        {
            Init(projectName);
            
            MakeProjectFile(targetToBuild, extraTarget);
            
            var result = _builder.Build();
            
            result.Should().Be(0);

            var shortName = Path.GetFileNameWithoutExtension(projectName);
            VerifyBuiltTargets($"{shortName}:{targetToBuild}", $"{shortName}:Build");
            
            return WriteCacheManifest();
        }
        
        [Fact]
        public void BuildProjectReference_IsCached()
        {
            var (fooManifestPath, _) = BuildAndCache("foo.csproj", "CacheMe");
            var fooPath = _context.ProjectFile;
            
            Init("bar.csproj");
            
            var builder = StartProject();
            builder.AppendLine($@"<Target Name='BuildReference'>
    <MSBuild Projects='{fooPath}' Targets='CacheMe'></MSBuild>
</Target>");
            builder.AppendLine($@"<ItemGroup>
    <ProjectReference Include='{fooPath}' />
</ItemGroup>
");
            EndProject(builder, "BuildReference");
            
            File.Move(fooManifestPath, _context.LabelPath(".cache_manifest"));
            
            var result = _builder.Build();
            
            result.Should().Be(0);
            
            VerifyBuiltTargets(
                /*foo:CacheMe is not built */
                "bar:BuildReference",
                "bar:Build"
                );
        }
        
        [Fact]
        public void TargetFrameworkAsGlobalProperty_IsCached()
        {
            Init("foo.csproj", "build");
            var builder = StartProject();
            builder.AppendLine($@"<Target Name='Pack'>
    <MSBuild Projects='$(MSBuildProjectFullPath)' 
            Targets='CacheMe'
            Properties='TargetFramework=netcoreapp3.1'
    ></MSBuild>
</Target>

<Target Name='CacheMe'>
    <Message Text='CacheMe.Built' Importance='High' />
</Target>

");
            EndProject(builder, "CacheMe");
            
            var result = _builder.Build();
            
            result.Should().Be(0);
            VerifyBuiltTargets(
                "foo:CacheMe",
                "foo:Build"
            );
            WriteCacheManifest();
            Init("foo.csproj", "pack");
            
            result = _builder.Build();

            result.Should().Be(0);
            VerifyBuiltTargets(
                "foo:Pack"
                 /* CacheMe should not be built */
                );
        }
        
        [Fact]
        public void CachedReference_NewTarget_Works()
        {
            // this is what happens with the GetTargetFrameworks reference in a normal Build.
            // GetTargetFrameworks is not built by the primary build process, but is instead built by references.
            
            var (fooManifestPath, fooManifest) = BuildAndCache("foo.csproj", "CacheMe", "ReferenceMe");
            var fooPath = _context.ProjectFile;
            Init("bar.csproj");
            
            var builder = StartProject();
            builder.AppendLine($@"<Target Name='BuildReference'>
    <MSBuild Projects='{fooPath}' Targets='ReferenceMe'></MSBuild>
</Target>");
            builder.AppendLine($@"<ItemGroup>
    <ProjectReference Include='{fooPath}' />
</ItemGroup>
");
            EndProject(builder, "BuildReference");
            
            File.Move(fooManifestPath, _context.LabelPath(".cache_manifest"));
            
            // The stock version of MSBuild throws a "caches should not overlap" internal exception when we build this 
            // way (when its compiled in debug mode)
            
            var result = _builder.Build();
            
            result.Should().Be(0);
            VerifyBuiltTargets(
                "bar:BuildReference",
                "foo:ReferenceMe",
                "bar:Build"
                );
            
            // make sure we serialized *only* the results from the last build we did, not cached results from previous
            // builds

            var (barManifestPath, barManifest) = WriteCacheManifest(fooManifest);
            Init("wow.csproj");
            File.Move(barManifestPath, _context.LabelPath(".cache_manifest"));
            MakeProjectFile("_");
            var _ = _builder.BeginBuild();

            var targetsInCache = _context.TargetGraph!.Nodes.Values
                .Concat(_context.TargetGraph.Clusters.SelectMany(c => c.Value.Nodes.Values))
                .Select(n => $"{Path.GetFileNameWithoutExtension(n.Cluster!.Name)}:{n.Name}")
                .ToHashSet();


            targetsInCache.OrderBy(t => t).Should().Equal(new[]
                {
                    "foo:Build",
                    "foo:CacheMe",
                    "foo:ReferenceMe",
                    "bar:BuildReference",
                    "bar:Build"
                }.OrderBy(t => t)
            );
        }

        private void MakeProjectFile(string targetName, string extraTarget = null)
        {
            var builder = StartProject();

            var targets = new List<string>() {targetName};
            if (extraTarget != null)
                targets.Add(extraTarget);
            
            foreach (var target in targets)
            {
                AddTarget(builder, target);
            }

            EndProject(builder, targetName);
        }

        private void EndProject(StringBuilder builder, params string[] targetNames)
        {
            builder.AppendLine($@" 
    <Target Name='Build' DependsOnTargets='{string.Join(";", targetNames)}'>
        <Message Text='Foo' Importance='High' />
    </Target>
</Project>");
            var contents = builder.ToString();
            File.WriteAllText(_context.ProjectFile,contents);
        }

        private static StringBuilder StartProject()
        {
            var builder = new StringBuilder(@"<Project>
    <PropertyGroup>
        <TargetFramework>netcoreapp3.1</TargetFramework>
    </PropertyGroup>

");
            return builder;
        }

        private static void AddTarget(StringBuilder builder, string targetName)
        {
            builder.AppendLine($@"    <Target Name='{targetName}'>
        <Message Text='{targetName}.Built' Importance='High' />
    </Target>
");
        }

        private (string path, CacheManifest cacheManifest) WriteCacheManifest(CacheManifest? other = null)
        {
            var cacheManifest = new CacheManifest()
            {
                Projects = new Dictionary<string, string>()
                {
                    [_builder.PathMapper.ToManifestPath(_context.ProjectFile)] = _context.OutputPath(Path.GetFileName(_context.ProjectFile) +$".{_context.Command.Action}.cache")
                }
            };
            if (other != null)
            {
                cacheManifest.Results.AddRange(other!.Results);
            }
            cacheManifest.Results.Add(_context.LabelPath(".cache"));
            
            var path = _context.LabelPath(".cache_manifest");
            File.WriteAllText(path, JsonConvert.SerializeObject(cacheManifest));
            return (path, cacheManifest);
        }

        public void VerifyBuiltTargets(params string[] targets)
        {
            _targetLog.Should().Equal(targets);
        }

        private  void VerifyProjectTargets(string projectName, params (string targetName, bool shouldBeBuilt)[] expectations)
        {
            expectations = expectations.Where(e => e.targetName != null).ToArray();
            var name = _builder.PathMapper.ToBazel(projectName);
            var cluster = _context.TargetGraph!.Clusters[name];
            cluster.Nodes.Count.Should().Be(expectations.Length, 
                $"{String.Join(";", cluster.Nodes.Keys)} should be {string.Join(";", expectations.Select(e => e.targetName))}");

            foreach (var expectation in expectations)
            {
                cluster.Nodes.Should().ContainKey(expectation.targetName);
                var node = cluster.Nodes[expectation.targetName];
                node.WasBuilt.Should().Be(expectation.shouldBeBuilt, $"Target: '{name}.{expectation.targetName}' WasBuilt should be '{expectation.shouldBeBuilt}'");
            }
        }

        public void Dispose()
        {
            foreach (var line in _log)
            {
                _helper.WriteLine(line.TrimEnd());    
            }
            
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