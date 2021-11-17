#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.BackEnd;
using RulesMSBuild.Tools.Builder.Diagnostics;
using RulesMSBuild.Tools.Builder.MSBuild;

namespace RulesMSBuild.Tools.Builder
{
    // https://stackoverflow.com/questions/64749385/predefined-type-system-runtime-compilerservices-isexternalinit-is-not-defined
    internal static class IsExternalInit
    {
    }

    public class BuildContext
    {
        public void MakeTargetGraph(bool force = false)
        {
            var trimPath = Bazel.ExecRoot + Path.DirectorySeparatorChar;
            if (DiagnosticsEnabled || force)
            {
                TargetGraph = new TargetGraph(trimPath, ProjectFile, null);
            }
        }

        public BuildCommand Command { get; }

        public void SetEnvironment()
        {
            foreach (var (name, value) in MSBuild.BuildEnvironment)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public string LabelPath(string extension) => Path.Combine(Bazel.OutputDir, Bazel.Label.Name) + extension;
        public string OutputPath(params string[] subpath) => Path.Combine(subpath.Prepend(Bazel.OutputDir).ToArray());
        public string ExecPath(string subpath) => Path.Combine(Bazel.ExecRoot, subpath);

        public string BinPath(string subpath) => Path.Combine(Bazel.BinDir, subpath);

        // msbuild auto-imports <project-file>.*.props from the restore dir from Microsoft.Common.Props
        public string ProjectExtensionPath(string extension) =>
            Path.Combine(MSBuild.RestoreDir, Path.GetFileName(ProjectFile) + extension);

        // This should be Bazel.ExecRoot, as all the sdk tools are in the sandbox with the builder when it is running
        // MSBuild, however, MSBuild appears to not like the sandbox for the locations of the SDK files as it produces
        //  Microsoft.Common.CurrentVersion.targets(2182,5): error MSB3095: Invalid argument. "DefiningProjectDirectory"
        //  is a reserved item metadata, and cannot be modified or deleted.
        // when run with ExecRoot. After much debugging, this appears to happen somewhere in ResolveAssemblyReferences 
        // because MSBuild thinks it is necessary to copy metdata from one TaskItem for a referenced project to another 
        // TaskItem for that referenced project because it is missing metadata before being resolved. 
        private string ToolPath(string subpath) => Path.Combine(Bazel.OutputBase, subpath);

        public BuildContext(BuildCommand command)
        {
            Command = command;
            Bazel = new BazelContext(command);
            NuGetConfig = ToolPath(command.nuget_config);
            Tfm = command.tfm;
            MSBuild = new MSBuildContext(
                command,
                Bazel,
                NuGetConfig,
                ToolPath(command.directory_bazel_props)
            );

            IsExecutable = command.output_type == "exe";

            SdkRoot = ToolPath(command.sdk_root);

            ProjectFile = ExecPath(command.project_file);
            ProjectDirectory = Path.GetDirectoryName(ProjectFile)!;

            IsTest = !string.IsNullOrEmpty(command.is_test);

            ProjectBazelProps = new Dictionary<string, string>()
            {
                ["Workspace"] = Bazel.Label.Workspace
            };

            void TrySetProp(string? value, string name)
            {
                value = value?.Trim('\'', '"');
                if (!string.IsNullOrEmpty(value))
                    ProjectBazelProps![name] = value;
            }

            TrySetProp(command.version, "Version");
            TrySetProp(command.package_version, "PackageVersion");

            if (DiagnosticsEnabled)
            {
                MSBuild.BuildEnvironment["NUGET_SHOW_STACK"] = "true";
            }

            MakeTargetGraph();
        }

        public Dictionary<string, string> ProjectBazelProps { get; }
        public bool IsExecutable { get; }
        public string ProjectDirectory { get; }

        // ReSharper disable once InconsistentNaming
        public MSBuildContext MSBuild { get; }
        public string ProjectFile { get; }
        public BazelContext Bazel { get; }
        public string NuGetConfig { get; }
        public string Tfm { get; set; }
        public string SdkRoot { get; }
        public bool DiagnosticsEnabled { get; set; } = Environment.GetEnvironmentVariable("BUILD_DIAG") == "1";
        public bool IsTest { get; }
        public TargetGraph? TargetGraph { get; set; }
        public string? Version { get; }
        public string? PackageVersion { get; }
        public string WorkspacePath(string path) => "/" + path[Bazel.ExecRoot.Length..];
    }

    public class BazelContext
    {
        public class BazelLabel
        {
            public BazelLabel(string workspace, string package, string name)
            {
                Workspace = workspace;
                Package = package;
                Name = name;
            }

            public string Workspace = null!;
            public string Package = null!;
            public string Name = null!;
            private string? _str;
            public bool IsExternal { get; set; }

            public override string ToString()
            {
                if (_str != null) return _str;
                _str = $"@{Workspace}//{Package}:{Name}";
                return _str;
            }

            public BazelLabel()
            {
            }
        }

        public BazelContext(BuildCommand command)
        {
            OutputBase = Path.GetFullPath(command.bazel_output_base);
            // bazel invokes us at $output_base/sandbox/darwin-sandbox/17/execroot/<workspace_name>
            ExecRoot = command.ExecRoot ?? Directory.GetCurrentDirectory();
            BinDir = Path.GetFullPath(Path.Combine(ExecRoot, command.bazel_bin_dir));

            Label = new BazelLabel(
                command.workspace,
                command.package,
                command.label_name);

            var outputSuffix = Label.Package;
            if (command.project_file.StartsWith("external"))
            {
                Label.IsExternal = true;
                outputSuffix = Path.Combine("external", Label.Workspace, outputSuffix);
            }

            OutputDir = Path.GetFullPath(Path.Combine(BinDir, outputSuffix));
        }

        public string OutputBase { get; }
        public string OutputDir { get; }
        public BazelLabel Label { get; }
        public string BinDir { get; }
        public string ExecRoot { get; set; }
    }
}