using System;
using System.IO;
using FluentAssertions;
using RulesMSBuild.Tools.Builder;
using RulesMSBuild.Tools.Builder.Launcher;
using Xunit;

namespace RulesMSBuild.Tests.Tools
{
    [Collection(BuildFrameworkTestCollection.TestCollectionName)]
    public class PathMapperTests
    {
        private static PathMapper Init(string input)
        {
            PathMapper.ResetInstance();
            var pathMapper = input.Contains('\\')
                ? new PathMapper(@"C:\o", @"C:\o\e")
                : new PathMapper("/o", "/o/e");
            return pathMapper;
        }
        
        [Theory]
        [InlineData(@"C:\o\e\file.txt", @"$exec_root\file.txt")]
        [InlineData(@"/o/e/file.txt", @"$exec_root/file.txt")]
        [InlineData(@"C:\o\file.txt", @"$output_base\file.txt")]
        [InlineData(@"/o/file.txt", @"$output_base/file.txt")]
        public void ToBazel_Works(string input, string expected)
        {
            var pathMapper = Init(input);

            var actual = pathMapper.ToBazel(input);

            actual.Should().Be(expected);
        }
        
        [Theory]
        [InlineData(@"C:\o\e\a\file.txt", @"a/file.txt")]
        [InlineData(@"/o/e/a/file.txt", @"a/file.txt")]
        public void ToManifestPath_Works(string input, string expected)
        {
            var pathMapper = Init(input);

            var actual = pathMapper.ToManifestPath(input);

            actual.Should().Be(expected);
        }
        
        [Theory]
        [InlineData(@"$exec_root\file.txt", @"C:\o\e\file.txt")]
        [InlineData(@"$exec_root/file.txt", @"/o/e/file.txt")]
        [InlineData(@"$output_base\file.txt", @"C:\o\file.txt")]
        [InlineData(@"$output_base/file.txt", @"/o/file.txt")]
        public void FromBazel_Works(string input, string expected)
        {
            var pathMapper = Init(expected);

            var actual = pathMapper.FromBazel(input);

            actual.Should().Be(expected);
        }
    }
}
