using System;
using System.Collections.Generic;
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

        private string NormalizePath(string input)
        {
            if (!_normalizePath) return input;
            return input.Replace('/', Path.DirectorySeparatorChar);
        }

        public BuildContext()
        {
            // for testing
            Command = new Command();
        }

        public void SetEnvironment()
        {
            var vars = new Dictionary<string, string>()
            {
                ["DirectoryBuildPropsPath"] = DirectoryBazelProps,
                ["ExecRoot"] = Bazel.ExecRoot,
                ["BINDIR"] = Bazel.BinDir,
                ["RestoreConfigFile"] = NuGetConfig,
            };
            
            if (Command.Action == "publish")
            {
                vars["PublishDir"] = Path.Combine(MSBuild.OutputPath, "publish", Tfm) + "/";
                // Required, otherwise publish will try to re-build and discard the previous build results
                vars["NoBuild"] = "true";
            }

            foreach (var (name, value) in vars)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public string LabelPath(string extension) => Path.Combine(Bazel.OutputDir, Bazel.Label.Name) + extension;
        public string OutputPath(string subpath) => Path.Combine(Bazel.OutputDir, subpath);
        private string ExecPath(string subpath) => Path.Combine(Bazel.ExecRoot, subpath);
        public string BinPath(string subpath) => Path.Combine(Bazel.BinDir, subpath);
        
        // This should be Bazel.ExecRoot, as all the sdk tools are in the sandbox with the builder when it is running
        // MSBuild, however, MSBuild appears to not like the sandbox for the locations of the SDK files as it produces
        //  Microsoft.Common.CurrentVersion.targets(2182,5): error MSB3095: Invalid argument. "DefiningProjectDirectory"
        //  is a reserved item metadata, and cannot be modified or deleted.
        // when run with ExecRoot. After much debugging, this appears to happen somewhere in ResolveAssemblyReferences 
        // because MSBuild thinks it is necessary to copy metdata from one TaskItem for a referenced project to another 
        // TaskItem for that referenced project because it is missing metadata before being resolved. 
        private string ToolPath(string subpath) => Path.Combine(Bazel.OutputBase, subpath);
        
        public BuildContext(Command command)
        {
            Command = command;
            _normalizePath = Path.DirectorySeparatorChar != BazelPathChar;
            Bazel = new BazelContext(command);
            MSBuild = new MSBuildContext(Bazel.OutputDir, command.Action);
            
            SdkRoot = ToolPath(command.NamedArgs["sdk_root"]);
            DirectoryBazelProps = ToolPath(command.NamedArgs["directory_bazel_props"]);
            
            ProjectFile = ExecPath(command.NamedArgs["project_file"]);
            NuGetConfig = ExecPath(command.NamedArgs["nuget_config"]);
            Tfm = command.NamedArgs["tfm"];
            IsTest = command.NamedArgs.TryGetValue("is_test", out _);
        }

        public MSBuildContext MSBuild { get; set; }
        public string ProjectFile { get; set; }
        public BazelContext Bazel { get; set; }
        public string NuGetConfig { get; set; }
        public string Tfm { get; set; }
        public string SdkRoot { get; set; }
        public bool DiagnosticsEnabled { get; set; }
        // todo(#51) disable when no build diagnostics are requested
        public bool BinlogEnabled { get; set; } = true;
        public string DirectoryBazelProps { get; set; }
        public bool IsTest { get; set; }

        public string WorkspacePath(string path) => "/" + path[Bazel.ExecRoot.Length..];
    }
    
    public class BazelContext
    {
        public class BazelLabel
        {
            public string Workspace { get; set; }
            public string Package { get; set; }
            public string Name { get; set; }
        }
        public BazelContext(Command command)
        {
            OutputBase = command.NamedArgs["bazel_output_base"];
            // bazel invokes us at the exec root
            ExecRoot = Directory.GetCurrentDirectory();
            // Suffix = ExecRoot[OutputBase.Length..];
            BinDir = Path.Combine(ExecRoot, command.NamedArgs["bazel_bin_dir"]);
                
            Label = new BazelLabel()
            {
                Workspace = command.NamedArgs["workspace"],
                Package = command.NamedArgs["package"],
                Name = command.NamedArgs["label_name"],
            };

            OutputDir = Path.Combine(BinDir, Label.Package);
        }

        public string OutputBase { get; set; }
        public string OutputDir { get; set; }
        public BazelLabel Label { get; set; }
        public string Suffix { get; set; }
        public string BinDir { get; set; }
        public string ExecRoot { get; set; }
            
    }
    
    public class MSBuildContext
    {
        public MSBuildContext(string outputDir, string action)
        {
            OutputPath = outputDir;
            BaseIntermediateOutputPath = Path.Combine(outputDir, "restore");
            IntermediateOutputPath = Path.Combine(outputDir, "obj");
            
            switch (action)
            {
                case "restore":
                    Targets = new[] {"Restore"};
                    break;
                case "build":
                    // https://github.com/dotnet/msbuild/issues/5204
                    Targets = new[]
                    {
                        "GetTargetFrameworks", 
                        "Build",
                        "GetCopyToOutputDirectoryItems",
                        "GetNativeManifest",
                        // included so Publish doesn't produce MSB3088
                        "ResolveAssemblyReferences"
                    };
                    break;
                case "publish":
                    Targets = new[] {"Publish"};
                    PostProcessCaches = false;
                    break;
                default:
                    throw new ArgumentException($"Unknown action {action}");
            }
        }

        public bool PostProcessCaches { get; set; } = true;

        public string[] Targets { get; set; }

        public string BaseIntermediateOutputPath { get; set; }
        public string IntermediateOutputPath { get; set; }
        public string OutputPath { get; set; }
    }
}