using FluentAssertions;
using RulesMSBuild.Tools.Bazel;
using Xunit;

namespace RulesMSBuild.Tools.RunfilesTests
{
    public class LabelTests
    {
        [Theory]
        [InlineData("//foo/bar:bam", "foo/bar", "bam")]
        [InlineData(":bar/bam/baz", "", "bar/bam/baz")]
        [InlineData("bar:bam/baz", "bar", "bam/baz")]
        [InlineData("//:bar/bam/baz", "", "bar/bam/baz")]
        [InlineData("//bar/bam", "bar/bam", "bam")]
        public void PackageAndName_Works(
            string rawValue, string package, string name)
        {
            var label = new Label(rawValue);
            label.IsValid.Should().Be(true);
            label.Workspace.Should().BeSameAs(Label.DefaultWorkspace);

            label.Package.Should().Be(package);
            label.Name.Should().Be(name);
        }

        [Theory]
        [InlineData("@foo//bar/bam:baz", "foo", "bar/bam", "baz")]
        public void Workspace_Works(string rawValue, string repoName, string package, string name)
        {
        }
    }
}