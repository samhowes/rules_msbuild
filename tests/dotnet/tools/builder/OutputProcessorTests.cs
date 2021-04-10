using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using MyRulesDotnet.Tools.Builder;
using Newtonsoft.Json;
using Xunit;

namespace MyRulesDotnet.Tests.Tools
{
    internal class FakeCommandOutputProcessor : OutputProcessor
    {
        public FakeCommandOutputProcessor(ProcessorContext context) : base(context)
        {
        }

        protected override void Fail(string message)
        {
            Assert.False(true, message);
        }

        protected override void RunCommand()
        {
            // don't run the command
        }
    }

    public class OutputProcessorTests : IDisposable
    {
        private string _contents = @"C:\foo\bar\bam";
        private readonly string _assetsFilepathBase;
        private readonly string _testDir;
        private readonly ProcessorContext _context;
        private OutputProcessor _processor;

        public OutputProcessorTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "obj");
            Directory.CreateDirectory(_testDir);
            _assetsFilepathBase = Path.Combine(_testDir, "project.assets");

            _context = new ProcessorContext()
            {
                TargetDirectory = _testDir,
                OutputDirectory = Path.Combine(_testDir, "processed"),
                OutputBase = Path.GetDirectoryName(_contents),
                ExecRoot = Path.Combine(_contents, "baz")
            };
            _processor = new FakeCommandOutputProcessor(_context);
        }

        public void Dispose()
        {
            Directory.Delete(_testDir, true);
        }


        [Theory]
        [InlineData(".json", true)]
        [InlineData(".props", false)]
        public void Process_HandlesWindowsPaths(string fileExtension, bool shouldBeEscaped)
        {
            string separator = @"\";
            if (shouldBeEscaped)
            {
                _contents = _contents.Replace(@"\", @"\\");
                separator = @"\\";
            }

            var testFileInput = _assetsFilepathBase + fileExtension;
            File.WriteAllText(testFileInput, _contents);

            _processor.PostProcess();

            var outputFile = Path.Combine(Path.GetDirectoryName(testFileInput)!, "processed",
                Path.GetFileName(testFileInput)!);

            var postProcessedContents = File.ReadAllText(outputFile);

            postProcessedContents.Should().Be(string.Join(separator, "$output_base$", "bam"));

            _context.TargetDirectory = Path.Combine(_context.TargetDirectory, "preprocessed", "obj");
            Directory.CreateDirectory(_context.TargetDirectory);

            _processor.PreProcess();

            outputFile = Path.Combine(_context.TargetDirectory, Path.GetFileName(testFileInput)!);

            var preProcessedContents = File.ReadAllText(outputFile);

            preProcessedContents.Should().Be(_contents);
        }


    }
}
