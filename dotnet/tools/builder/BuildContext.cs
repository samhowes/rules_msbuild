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
            foreach (var (name, value) in MSBuild.BuildEnvironment)
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
            NuGetConfig = ExecPath(command.NamedArgs["nuget_config"]);
            Tfm = command.NamedArgs["tfm"];
            MSBuild = new MSBuildContext(
                command.Action,
                Bazel,
                ToolPath(command.NamedArgs["directory_bazel_props"]),
                NuGetConfig,
                Tfm
                );
            
            SdkRoot = ToolPath(command.NamedArgs["sdk_root"]);
            
            ProjectFile = ExecPath(command.NamedArgs["project_file"]);
            
            
            IsTest = command.NamedArgs.TryGetValue("is_test", out _);
        }

        // ReSharper disable once InconsistentNaming
        public MSBuildContext MSBuild { get; }
        public string ProjectFile { get; }
        public BazelContext Bazel { get; }
        public string NuGetConfig { get; }
        public string Tfm { get; }
        public string SdkRoot { get; }
        public bool DiagnosticsEnabled { get; set; }
        // todo(#51) disable when no build diagnostics are requested
        public bool BinlogEnabled { get; } = true;
        public bool IsTest { get; }

        public string WorkspacePath(string path) => "/" + path[Bazel.ExecRoot.Length..];
    }
    
    public class BazelContext
    {
        public class BazelLabel
        {
            public string Workspace { get; init; }
            public string Package { get; init; }
            public string Name { get; init; }
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

        public string OutputBase { get; }
        public string OutputDir { get; }
        public BazelLabel Label { get; }
        public string Suffix { get; set; }
        public string BinDir { get; }
        public string ExecRoot { get; }
            
    }
    
    // ReSharper disable once InconsistentNaming
    public class MSBuildContext
    {
        public MSBuildContext(string action, BazelContext bazel, string directoryBazelPropsPath, string nuGetConfig, string tfm)
        {
            OutputPath = bazel.OutputDir;
            BaseIntermediateOutputPath = Path.Combine(OutputPath, "restore");
            IntermediateOutputPath = Path.Combine(OutputPath, "obj");
            
            BuildEnvironment = new Dictionary<string, string>()
            {
                ["DirectoryBuildPropsPath"] = directoryBazelPropsPath,
                ["ExecRoot"] = bazel.ExecRoot,
                ["BINDIR"] = bazel.BinDir,
                ["RestoreConfigFile"] = nuGetConfig,
                ["PublishDir"] = Path.Combine(OutputPath, "publish", tfm) + "/",
            };
            
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
                    
                    // Required, otherwise publish will try to re-build and discard the previous build results
                    BuildEnvironment["NoBuild"] = "true";
                    UseCaching = false;
                    // msbuild is going to evaluate all the project files anyways, and we can't use any input caches
                    // from the builds, so just do a graph build. It might be faster to use publish caches, but this 
                    // current implementation is quite slower than a standard `dotnet publish /graph` anyways, so 
                    // i'll be taking more of a look at optimizations later. 
                    GraphBuild = true;
                    
                    break;
                default:
                    throw new ArgumentException($"Unknown action {action}");
            }
        }

        public Dictionary<string,string> BuildEnvironment { get; }

        public bool GraphBuild { get; }

        public bool UseCaching { get; } = true;

        public string[] Targets { get; }

        public string BaseIntermediateOutputPath { get; }
        public string IntermediateOutputPath { get; }
        public string OutputPath { get; }
    }
}