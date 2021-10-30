using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using NuGetParser;
using Xunit;

namespace NuGetParserTests
{
    public class AssetsReaderTests
    {
        const string ObjDirectory = "foo-obj";
        const string Tfm = "net5.0";
        const string DotnetRoot = "dotnet_root";

        [Theory]                    // 2.8.0 is in the assets file
        [InlineData("2.9.0", 0)]    // higher override version means bazel won't see the files in the nuget folder
        [InlineData("2.7.0", 15)]   // lower means we'll actually download the requested package
        [InlineData("2.8.0", 0)]    // equal means no download
        public void Overrides_IgnoreFiles(string overrideVersion, int expectedFileCount)
        {
            var json = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "project.assets.json"));
            var files = new Mock<Files>();
            files.Setup(f => f.GetContents(Path.Combine(ObjDirectory, "project.assets.json")))
                .Returns(json);

            var overridesPath = Path.Combine(
                DotnetRoot, "packs", "Microsoft.NETCore.App.Ref", "5.0.0", "data", "PackageOverrides.txt");
            files.Setup(f => f.ReadAllLines(overridesPath))
                .Returns(new []{$"CommandLineParser|{overrideVersion}"});

            var assetsReader = new AssetsReader(files.Object, DotnetRoot);

            var errorMessage = assetsReader.Init(ObjDirectory, Tfm);
            errorMessage.Should().BeNull();

            var versions = assetsReader.GetPackages().ToList();

            versions.Count.Should().Be(1);
            var version = versions.Single();

            version.AllFiles.Count.Should().Be(expectedFileCount);
        }
    }
}