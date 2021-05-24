using System;
using System.IO;
using static MyRulesDotnet.Tools.Builder.BazelLogger;

namespace MyRulesDotnet.Tools.Builder
{
    public class BuildContext
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

        public BuildContext()
        {
            Command = new Command();
        }

        public BuildContext(Command command)
        {
            Command = command;

            _normalizePath = Path.DirectorySeparatorChar != BazelPathChar;
            BazelOutputBase = NormalizePath(command.NamedArgs["bazel_output_base"]);
            GeneratedProjectFile = Path.GetFullPath(NormalizePath(command.NamedArgs["generated_project_file"]));
            SourceProjectFile = NormalizePath(command.NamedArgs["source_project_file"]);
            SdkRoot = NormalizePath(command.NamedArgs["sdk_root"]);
            Tfm = NormalizePath(command.NamedArgs["tfm"]);
            NuGetConfig = Path.GetFullPath(command.NamedArgs["nuget_config"]);
            
            // these may not be necessary
            Package = command.NamedArgs["package"];
            Workspace = command.NamedArgs["workspace"];
            LabelName = command.NamedArgs["label_name"];
            
            // (accurately) assumes bazel invokes actions at ExecRoot
            ExecRoot = Directory.GetCurrentDirectory();
            
            Validate();
            
            ProjectDirectory = Path.GetDirectoryName(GeneratedProjectFile)!;
            IntermediateBase = NormalizePath(Path.Combine(ProjectDirectory!, "obj"));
            OutputDirectory = NormalizePath(Path.Combine(ProjectDirectory, Tfm));
        }

        public string NuGetConfig { get; set; }

        public string LabelName { get; set; }

        public string SourceProjectFile { get; set; }

        public string ProjectDirectory { get; set; }

        private void Validate()
        {
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

        public string GeneratedProjectFile { get; set; }

        public string IntermediateBase { get; set; }
        public string BazelOutputBase { get; set; }
        public string Suffix { get; set; }
        public string ExecRoot { get; set; }
        public string OutputDirectory { get; set; }
        public string SdkRoot { get; set; }
        public bool DiagnosticsEnabled { get; set; }
        // todo(#51) disable when no build diagnostics are requested
        public bool BinlogEnabled { get; set; } = true;
    }
}