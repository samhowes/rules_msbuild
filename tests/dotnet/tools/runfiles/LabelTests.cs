
using FluentAssertions;
using RulesMSBuild.Tools.Bazel;
using Xunit;

namespace RulesMSBuild.Tools.RunfilesTests
{
    public class LabelTests
    {
        [Theory]
        [InlineData("//foo/bar:bam", true, Label.DefaultWorkspace, "foo/bar", "bam", "foo/bar/bam")]
        [InlineData("@foo//bar/bam:baz", true, "foo", "bar/bam", "baz", "bar/bam/baz")]
        [InlineData(":bar/bam/baz", true, Label.DefaultWorkspace, "", "bar/bam/baz", "bar/bam/baz")]
        [InlineData("bar:bam/baz", true, Label.DefaultWorkspace, "bar", "bam/baz", "bar/bam/baz")]
        [InlineData("//:bar/bam/baz", true, Label.DefaultWorkspace, "", "bar/bam/baz", "bar/bam/baz")]
        [InlineData("//bar/bam", true, Label.DefaultWorkspace, "bar/bam", "", "bar/bam")]
        public void Parsing_Works(
            string rawValue, bool isValid, string workspace, string package, string target, string rpath)
        {
            var label = new Label(rawValue);
            label.IsValid.Should().Be(isValid);
            if (workspace == Label.DefaultWorkspace)
                label.Workspace.Should().BeSameAs(Label.DefaultWorkspace);
            else
                label.Workspace.Should().Be(workspace);
            
            label.Package.Should().Be(package);
            label.Name.Should().Be(target);
            label.RelativeRpath.Should().Be(rpath);
        }
    }
}