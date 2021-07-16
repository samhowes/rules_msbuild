using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.Build.Evaluation;
using Newtonsoft.Json;
using RulesMSBuild.Tools.Bazel;
using RulesMSBuild.Tools.Builder;
using RulesMSBuild.Tools.Builder.Diagnostics;
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

        private void Init(string projectName)
        {
            // keep as a separate method so we cn make sure to register the msbuild assemblies first.
            PathMapper.ResetInstance();
            _context = new BuildContext(new Command()
            {
                Action = "build",
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
            _builder = new Builder(_context, (message => _log.Add(message)));
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

            var built = _context.TargetGraph;
            built!.Clusters.Count.Should().Be(1);
            var cluster = built.Clusters.Values.Single();

            cluster.Nodes.Count.Should().Be(1);
            var node = cluster.Nodes.Values.Single();
            node.Name.Should().Be("Build");
        }
        
        [Fact]
        public void BuildFromCache_SkipsSecondTime()
        {
            Init("foo.csproj");
            MakeProjectFile("CacheMe");
            
            var result = _builder.Build();
            
            result.Should().Be(0);
            VerifyBuiltTargets(
                ("Build", true),
                ("CacheMe", true));
            WriteCacheManifest();
            Init("foo.csproj");

            result = _builder.Build();

            result.Should().Be(0);
            VerifyBuiltTargets(
                ("Build", false),
                ("CacheMe", false)
                );
        }

        private string BuildAndCache(string projectName,params string[] targets)
        {
            Init(projectName);
            MakeProjectFile(targets);
            
            var result = _builder.Build();
            
            result.Should().Be(0);
            VerifyBuiltTargets(targets.Append("Build").Select(t => (t, true)).ToArray());
            return WriteCacheManifest();
        }
        
        [Fact]
        public void BuildProjectReference_IsCached()
        {
            var fooManifest = BuildAndCache("foo.csproj", "CacheMe");
            var fooPath = _context.ProjectFile;
            var projectName = "bar.csproj";
            Init(projectName);
            
            var builder = StartProject();
            builder.AppendLine($@"<Target Name='BuildReference'>
    <MSBuild Projects='{fooPath}' Targets='CacheMe'></MSBuild>
</Target>");
            builder.AppendLine($@"<ItemGroup>
    <ProjectReference Include='{fooPath}' />
</ItemGroup>
");
            EndProject(builder, "BuildReference");
            
            File.Move(fooManifest, _context.LabelPath(".cache_manifest"));
            
            var result = _builder.Build();
            
            result.Should().Be(0);
            VerifyProjectTargets(_context.ProjectFile,
                ("BuildReference", true),
                ("Build", true));
            VerifyProjectTargets(fooPath, 
                ("Build", false),
                ("CacheMe", false));
            WriteCacheManifest();
        }

        private void MakeProjectFile(params string[] targetNames)
        {
            var builder = StartProject();

            // AddTarget(builder, "ReferenceOnly");
            
            foreach (var targetName in targetNames)
            {
                AddTarget(builder, targetName);
            }

            EndProject(builder, targetNames);
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

        private string WriteCacheManifest()
        {
            var cacheManifest = new CacheManifest()
            {
                Results = new Dictionary<string, string>()
                {
                    [_builder.PathMapper.ToManifestPath(_context.ProjectFile)] = _context.LabelPath(".cache")
                }
            };
            var path = _context.LabelPath(".cache_manifest");
            File.WriteAllText(path, JsonConvert.SerializeObject(cacheManifest));
            return path;
        }

        private void VerifyBuiltTargets(params (string targetName, bool shouldBeBuilt)[] expectations)
        {
            var built = _context.TargetGraph;
            built!.Clusters.Count.Should().Be(1);
            var cluster = built.Clusters.Values.Single();

            VerifyProjectTargets(cluster.Name, expectations);
        }

        private  void VerifyProjectTargets(string projectName, params (string targetName, bool shouldBeBuilt)[] expectations)
        {
            var name = _builder.PathMapper.ToBazel(projectName);
            var cluster = _context.TargetGraph!.Clusters[name];
            cluster.Nodes.Count.Should().Be(expectations.Length, String.Join(";", cluster.Nodes.Keys));

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