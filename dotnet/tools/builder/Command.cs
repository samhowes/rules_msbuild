using System.Collections.Generic;
using CommandLine;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace RulesMSBuild.Tools.Builder
{
    [Verb("launcher")]
    public class LauncherCommand
    {
        [Value(1, Required = true)] public IList<string> Args { get; set; }
    }

    [Verb("inspect")]
    public class InspectCommand
    {
        [Value(1, Required = true)] public string File { get; set; }
    }

    [Verb("build", isDefault: true)]
    public class BuildCommand
    {
        [Value(0, Required = true)] public string Action { get; set; }

        [Option("sdk_root", Required = true)] public string sdk_root { get; set; }

        [Option("project_file", Required = true)]
        public string project_file { get; set; }

        [Option("bazel_bin_dir", Required = true)]
        public string bazel_bin_dir { get; set; }

        [Option("tfm", Required = true)] public string tfm { get; set; }

        [Option("bazel_output_base", Required = true)]
        public string bazel_output_base { get; set; }

        [Option("workspace", Required = true)] public string workspace { get; set; }

        [Option("package", Required = true)] public string package { get; set; }

        [Option("label_name", Required = true)]
        public string label_name { get; set; }

        [Option("assembly_name", Required = true)]
        public string assembly_name { get; set; }

        [Option("nuget_config", Required = true)]
        public string nuget_config { get; set; }

        [Option("directory_bazel_props", Required = true)]
        public string directory_bazel_props { get; set; }

        [Option("configuration", Required = true)]
        public string configuration { get; set; }

        [Option("output_type", Required = true)]
        public string output_type { get; set; }

        [Option("launcher_template")] public string LauncherTemplate { get; set; }

        [Option("version", Required = false)] public string version { get; set; }

        [Option("package_version", Required = false)]
        public string package_version { get; set; }

        [Option("is_test", Required = false)] public string is_test { get; set; }

        [Option("runfiles_manifest", Required = false)]
        public string RunfilesManifest { get; set; }


        [Option("directory")] public IEnumerable<string> DirectorySrcs { get; set; }
    }
}