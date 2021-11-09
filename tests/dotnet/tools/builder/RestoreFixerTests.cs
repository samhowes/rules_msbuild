using System;
using System.IO;
using FluentAssertions;
using Moq;
using RulesMSBuild.Tests.Tools;
using RulesMSBuild.Tools.Builder;
using Xunit;

namespace BuilderTests
{
    public class PathTheoryAttribute : TheoryAttribute
    {
        public PathTheoryAttribute(char separtor)
        {
            if (separtor != Path.DirectorySeparatorChar)
                Skip = $"Wrong path separator ({Path.DirectorySeparatorChar}) for test";
        }
    }

    public class RestoreFixerTests
    {
        private readonly Mock<Files> _files;
        private BuildContext _context;
        private RestoreFixer _fixer;
        private string _contents;
        private readonly MemoryStream _bazelOutStream;
        private readonly MemoryStream _ideOutStream;
        private string _bazelOut;
        private string _ideOut;

        public RestoreFixerTests()
        {
            _files = new Mock<Files>();
            MakeContext(false);

            _fixer = new RestoreFixer(_context, _files.Object, new Paths());
            _files.Setup(f => f.GetContents(It.IsAny<string>())).Returns(() => _contents);

            var c = Path.DirectorySeparatorChar;
            _bazelOutStream = new UnclosableMemoryStream();
            _ideOutStream = new UnclosableMemoryStream();
            _files.Setup(f => f.Create(It.IsAny<string>()))
                .Returns<string>(path => path.Contains($"{c}_{c}") ? _bazelOutStream : _ideOutStream);
        }

        private void MakeContext(bool windows)
        {
            string Path(string p) => windows ? "C:" + p : p;
            _context = new BuildContext(new BuildCommand()
            {
                bazel_output_base = Path("/output_base"),
                project_file = Path("/output_base/sandbox/5/execroot/main/foo/foo.csproj"),
                ExecRoot = windows ? @"C:\output_base\sandbox\5\execroot\main" : "/output_base/sandbox/5/execroot/main",
                bazel_bin_dir = "bazel-bin",
                package = "foo",
                nuget_config = "nuget.config",
                directory_bazel_props = "bazel_props.props",
                tfm = "net5.0",
                configuration = "debug",
                DirectorySrcs = ArraySegment<string>.Empty,
                Action = "restore",
                sdk_root = "sdk_root",
            });
        }

        [PathTheory('/')]
        [InlineData(
            "{foo: /output_base/sandbox/5/execroot/main/wow<",
            "{foo: ../wow<",
            "{foo: /output_base/execroot/main/wow<")]
        [InlineData(
            "{foo: /output_base/execroot/main/wow<",
            "{foo: ../../../../../execroot/main/wow<",
            "{foo: /output_base/execroot/main/wow<")]
        [InlineData(
            "foo: /output_base/sandbox/5/execroot/main/wow<",
            "foo: $(ExecRoot)/wow<",
            "foo: /output_base/execroot/main/wow<")]
        [InlineData(
            "foo: /output_base/execroot/main/wow<",
            "foo: $(ExecRoot)/../../../../execroot/main/wow<",
            "foo: /output_base/execroot/main/wow<")]
        [InlineData(
            "{foo: /output_base/sandbox/5/execroot/main/wow/_/<",
            "{foo: ../wow/_/<",
            "{foo: /output_base/execroot/main/wow/<")]
        [InlineData(
            "{foo: /output_base/sandbox/5/execroot/main/wow/_<",
            "{foo: ../wow/_<",
            "{foo: /output_base/execroot/main/wow<")]
        public void RestoreFixerWorks(string contents, string bazelout, string ideOut)
        {
            _contents = contents;
            Fix();
            _bazelOut.Should().Be(bazelout);
            _ideOut.Should().Be(ideOut);
        }

        [PathTheory('\\')]
        [InlineData(
            @"{foo: C:\\output_base\\sandbox\\5\\execroot\\main\\wow<",
            @"{foo: ..\\wow<",
            @"{foo: C:\\output_base\\execroot\\main\\wow<")]
        [InlineData(
            @"{foo: C:\\output_base\\execroot\\main\\wow<",
            @"{foo: ..\\..\\..\\..\\..\\execroot\\main\\wow<",
            @"{foo: C:\\output_base\\execroot\\main\\wow<")]
        [InlineData(
            @"{foo: C:\\output_base\\execroot\\main\\wow\\_\\<",
            @"{foo: ..\\..\\..\\..\\..\\execroot\\main\\wow\\_\\<",
            @"{foo: C:\\output_base\\execroot\\main\\wow\\<")]
        [InlineData(
            @"{foo: C:\\output_base\\execroot\\main\\wow\\_<",
            @"{foo: ..\\..\\..\\..\\..\\execroot\\main\\wow<",
            @"{foo: C:\\output_base\\execroot\\main\\wow<")]
        [InlineData(
            @"foo: C:\output_base\sandbox\5\execroot\main\wow<",
            @"foo: $(ExecRoot)\wow<",
            @"foo: C:\output_base\execroot\main\wow<")]
        [InlineData(
            @"foo: C:\output_base\execroot\main\wow<",
            @"foo: $(ExecRoot)\..\..\..\..\execroot\main\wow<",
            @"foo: C:\output_base\execroot\main\wow<")]
        [InlineData(
            @"foo: C:\output_base\execroot\main\wow\_<",
            @"foo: $(ExecRoot)\..\..\..\..\execroot\main\wow\_<",
            @"foo: C:\output_base\execroot\main\wow<")]
        public void EscapingOnWindowsWorks(string contents, string bazelout, string ideOut)
        {
            MakeContext(true);
            _fixer = new RestoreFixer(_context, _files.Object, new WindowsPaths());
            _contents = contents;
            Fix();
            _bazelOut.Should().Be(bazelout);
            _ideOut.Should().Be(ideOut);
        }


        private void Fix()
        {
            _fixer.Fix("foo");
            _bazelOutStream.Seek(0, SeekOrigin.Begin);
            _ideOutStream.Seek(0, SeekOrigin.Begin);
            _bazelOut = new StreamReader(_bazelOutStream).ReadToEnd();
            _ideOut = new StreamReader(_ideOutStream).ReadToEnd();
            _ideOut.Length.Should().BeGreaterThan(0);
            _bazelOut.Length.Should().BeGreaterThan(0);
        }
    }
}