using System;
using System.IO;
using System.Linq;
using Xunit;
using bootstrapper;
using FluentAssertions;
using System.Collections.Generic;

namespace bootstrapper_tests
{
    public class BuildWriterTests
    {
        private BuildWriter _writer;
        private string _buildContents;
        private StreamWriter _output;

        public BuildWriterTests()
        {
            _buildContents = @"
nuget_restore(
    name = ""restore"",
    target_frameworks = [
        # bootstrap:tfms
    ],
    deps = [
        # bootstrap:deps
    ],
)";
            _output = new StreamWriter(new MemoryStream());
            _output.Write(_buildContents);
            _output.Flush();
            _output.BaseStream.Seek(0, SeekOrigin.Begin);
            _writer = new BuildWriter(_output);
        }

        [Fact]
        public void Write_Works()
        {
            var targets = new List<DotnetTarget>()
            {
                new DotnetTarget()
                {
                    Label = new Label("//foo:bar")
                    {
                        WorkspaceName = "fam"
                    },
                    Tfms = new List<string>(){"netcoreapp3.1"}
                }
            };
            _writer.Write(_buildContents, targets);

            _output.BaseStream.Seek(0, SeekOrigin.Begin);

            var result = new StreamReader(_output.BaseStream).ReadToEnd();
            result.Should().Be(@"
nuget_restore(
    name = ""restore"",
    target_frameworks = [
        ""netcoreapp3.1"",
        # bootstrap:tfms
    ],
    deps = [
        ""@fam//foo:bar"",
        # bootstrap:deps
    ],
)");
        }
    }
}