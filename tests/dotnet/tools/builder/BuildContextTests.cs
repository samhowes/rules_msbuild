using System;
using System.IO;
using System.Linq;
using CommandLine;
using CommandLine.Text;
using FluentAssertions;
using RulesMSBuild.Tools.Builder;
using Xunit;

namespace RulesMSBuild.Tests.Tools
{
    public class BuildContextTests
    {
        [Theory]
        [InlineData("foo/bar/bar.csproj", "foo/bar/bar")]
        [InlineData("external/foo_workspace/foo/bar/bar.csproj", "external/foo_workspace/foo/bar/bar")]
        public void LabelPath_Works(string projectFilePath, string expectedLabelPath)
        {
            var cwd = Directory.GetCurrentDirectory();
            var bin = "bazel-out/fastbuild/bin";
            var workspace = "foo_workspace";
            var command = new BuildCommand()
            {
                Action = "build",
                project_file = projectFilePath,
                bazel_output_base = Path.GetDirectoryName(cwd!)!,
                bazel_bin_dir = bin,
                workspace = workspace,
                package = "foo/bar",
                label_name = "bar",
                nuget_config = "nuget.Config",
                directory_bazel_props = "Directory.Bazel.props",
                configuration = "debug",
                tfm = "netcoreapp3.1",
                sdk_root = "dotnet/sdk",
                DirectorySrcs = Array.Empty<string>()
            };
            var context = new BuildContext(command);

            var path = context.LabelPath("");

            var trimmed = path.Substring(cwd.Length +1);

            var fullExpected = Path.Combine(bin, expectedLabelPath);
            if (Path.DirectorySeparatorChar == '\\')
                fullExpected = fullExpected.Replace('/', '\\');

            trimmed.Should().Be(fullExpected);

        }
    }
}
