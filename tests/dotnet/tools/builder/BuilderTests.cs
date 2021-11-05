using System;
using System.IO;
using FluentAssertions;
using RulesMSBuild.Tools.Builder;
using RulesMSBuild.Tools.Builder.Launcher;
using Xunit;

namespace RulesMSBuild.Tests.Tools
{
    public class BuilderTests
    {
        [Fact]
        public void LaunchDataWriter_Works()
        {
            var stream = new MemoryStream();
            var writer = new LaunchDataWriter()
                .Add("foo", "bar")
                .Add("a", "b");

            writer.Save(stream);

            stream.Seek(0, SeekOrigin.Begin);



            var reader = new BinaryReader(stream);
            var span = new ReadOnlySpan<byte>(reader.ReadBytes((int) reader.BaseStream.Length));

            var expectedLaunchBytes = new byte[]
            {
                // https://unicodelookup.com/
                0x66, // f
                0x6F, // o
                0x6F, // o
                0x3D, // =
                0x62, // b
                0x61, // a
                0x72, // r
                0x00, // \0

                0x61, // a
                0x3D, // =
                0x62, // b
                0x00, // \0
            };

            var actualLaunchBytes = span.Slice(0, span.Length - sizeof(Int64)).ToArray();

            actualLaunchBytes.Should().Equal(expectedLaunchBytes);

            var launchDataLength = BitConverter.ToInt64(span.Slice(expectedLaunchBytes.Length).ToArray());
            launchDataLength.Should().Be(span.Length - sizeof(Int64));
        }
    }
}
