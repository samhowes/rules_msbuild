#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using NuGetParser;
using Xunit;

namespace NuGetParserTests
{
    public class AssetsReaderTests
    {
        private readonly Mock<Files> _files;
        private readonly AssetsReader _assetsReader;
        const string ObjDirectory = "foo-obj";
        const string Tfm = "net5.0";
        const string DotnetRoot = "dotnet_root";
        const string PackagesFolder = "packages_folder";

        public AssetsReaderTests()
        {
            _files = new Mock<Files>();
            _assetsReader = new AssetsReader(_files.Object, new NuGetContext(new Dictionary<string, string>()
            {
                ["packages_folder"] = PackagesFolder,
                ["dotnet_path"] = Path.Combine("foo", DotnetRoot),
            }));
        }

        [Theory] // 2.8.0 is in the assets file
        [InlineData("2.9.0", 15)]
        [InlineData("2.7.0", 15)]
        [InlineData("2.8.0", 15)]
        public void Overrides_IgnoreFiles(string overrideVersion, int expectedFileCount)
        {
            var json = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "project.assets.json"));

            _files.Setup(f => f.GetContents(Path.Combine(ObjDirectory, "project.assets.json")))
                .Returns(json);

            var overridesPath = Path.Combine(
                DotnetRoot, "packs", "Microsoft.NETCore.App.Ref", "5.0.0", "data", "PackageOverrides.txt");
            SetupOverrides(overrideVersion, overridesPath);

            var errorMessage = _assetsReader.Init(ObjDirectory, Tfm);
            errorMessage.Should().BeNull();

            var versions = _assetsReader.GetPackages().ToList();

            versions.Count.Should().Be(1);
            var version = versions.Single();

            version.AllFiles.Count.Should().Be(expectedFileCount);
        }

        private void SetupOverrides(string overrideVersion, string? overridesPath)
        {
            _files.Setup(f => f.ReadAllLines(overridesPath))
                .Returns(new[] { $"CommandLineParser|{overrideVersion}" });
        }

        [Fact]
        public void Overrides_AreLoadedFromPackage_WhenInDownloadDeps()
        {
            SetupAssets((json) =>
            {
                json["project"]["frameworks"]["net5.0"]["downloadDependencies"] = JArray.Parse(@"[
          {
            ""name"": ""Microsoft.NETCore.App.Ref"",
            ""version"": ""[3.1.0, 3.1.0]""
          }
        ]");
            });

            var errorMessage = _assetsReader.Init(ObjDirectory, Tfm);
            errorMessage.Should().BeNull();

            var path = $"{PackagesFolder}/microsoft.netcore.app.ref/3.1.0/data/PackageOverrides.txt";

            _files.Verify(f => f.ReadAllLines(path),
                Times.Exactly(1));

            var versions = _assetsReader.GetPackages().ToList();

            versions.Count.Should().Be(1);
            var version = versions.Single();

            // we should download this one because we downloaded the override
            version.AllFiles.Count.Should().Be(15);
        }

        private void SetupAssets(Action<JObject>? modify = null)
        {
            var json = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "project.assets.json"));
            var jobject = JObject.Parse(json);
            modify?.Invoke(jobject);
            json = jobject.ToString();

            _files.Setup(f => f.GetContents(Path.Combine(ObjDirectory, "project.assets.json")))
                .Returns(json);
        }
    }
}