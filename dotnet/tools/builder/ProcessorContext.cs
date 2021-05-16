using System;
using System.IO;
using static MyRulesDotnet.Tools.Builder.BazelLogger;

namespace MyRulesDotnet.Tools.Builder
{
    public class ProcessorContext
    {
        public Command Command { get; }

        // bazel always sends us POSIX paths
        private const char BazelPathChar = '/';
        private readonly bool _normalizePath;

        private const string OutputDirectoryKey = "output_directory";

        private string NormalizePath(string input)
        {
            if (!_normalizePath) return input;
            return input.Replace('/', Path.DirectorySeparatorChar);
        }

        // for testing

        public ProcessorContext()
        {
            Command = new Command();
        }

        public ProcessorContext(Command command)
        {
            Command = command;

            _normalizePath = Path.DirectorySeparatorChar != BazelPathChar;
            IntermediateBase = NormalizePath(command.NamedArgs["intermediate_base"]);
            BazelOutputBase = NormalizePath(command.NamedArgs["bazel_output_base"]);
            ProjectFile = NormalizePath(command.NamedArgs["project_file"]);
            SdkRoot = NormalizePath(command.NamedArgs["sdk_root"]);
            Package = command.NamedArgs["package"];
            Workspace = command.NamedArgs["workspace"];
            Tfm = NormalizePath(command.NamedArgs["tfm"]);
            // (accurately) assumes bazel invokes actions at ExecRoot
            ExecRoot = Directory.GetCurrentDirectory();
            ChildCommand = command.PassThroughArgs;
            if (command.NamedArgs.TryGetValue(OutputDirectoryKey, out var outputDirectory))
                OutputDirectory = outputDirectory;
            
            Validate();
        }
        
        private void Validate()
        {
            if (!IntermediateBase.EndsWith("obj"))
                Fail($"Refusing to process unexpected directory {IntermediateBase}");

            if (DebugEnabled)
            {
                Debug(Directory.GetCurrentDirectory());
                foreach (var entry in Directory.EnumerateDirectories("."))
                    Console.WriteLine(entry);
            }

            if (!ExecRoot.StartsWith(BazelOutputBase))
                Fail($"Refusing to process trim_path {BazelOutputBase} that is not a prefix of" +
                     $" cwd {ExecRoot}");

            Suffix = ExecRoot[BazelOutputBase.Length..];
        }

        public string Workspace { get; set; }

        public string Package { get; set; }

        public string Tfm { get; set; }

        public string ProjectFile { get; set; }

        public string IntermediateBase { get; set; }
        public string BazelOutputBase { get; set; }
        public string[] ChildCommand { get; set; }
        public string Suffix { get; set; }
        public string ExecRoot { get; set; }
        public string OutputDirectory { get; set; }
        public string SdkRoot { get; set; }
    }
}