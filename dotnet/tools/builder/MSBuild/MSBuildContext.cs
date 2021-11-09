using System;
using System.Collections.Generic;
using System.IO;

namespace RulesMSBuild.Tools.Builder.MSBuild
{
    // ReSharper disable once InconsistentNaming
    public class MSBuildContext
    {
        public MSBuildContext(BuildCommand command,
            BazelContext bazel,
            string nuGetConfig,
            string directoryBazelPropsPath)
        {
            Configuration = command.configuration;
            OutputPath = bazel.OutputDir;
            PublishDir = Path.Combine(OutputPath, "publish", command.tfm);
            BaseIntermediateOutputPath = Path.Combine(OutputPath, "restore");
            IntermediateOutputPath = Path.Combine(OutputPath, "obj");

            var propsDirectory = Path.GetDirectoryName(directoryBazelPropsPath);

            var
                noWarn = "NU1603"; // Microsoft.TestPlatform.TestHost 16.7.1 depends on Newtonsoft.Json (>= 9.0.1) but Newtonsoft.Json 9.0.1 was not found. An approximate best match of Newtonsoft.Json 13.0.1 was resolved.

            GlobalProperties = new Dictionary<string, string>
            {
                ["Configuration"] = Configuration,
                ["BuildProjectReferences"] = "false",
                // enables a faster nuget restore compatible with isolated builds
                // https://github.com/NuGet/NuGet.Client/blob/21e2a87537cd9655b7f6599af013d447aa058e29/src/NuGet.Core/NuGet.Build.Tasks/NuGet.targets#L1310
                ["RestoreUseStaticGraphEvaluation"] = "true",
                ["RestoreRecursive"] = "false", // only restore the entry project, not referenced projects
            };

            switch (Configuration.ToLower())
            {
                case "debug":
                    GlobalProperties["DebugSymbols"] = "true";
                    break;
                case "release":
                case "fastbuild":
                    GlobalProperties["DebugSymbols"] = "false";
                    GlobalProperties["DebugType"] = "none";
                    break;
            }

            BuildEnvironment = new Dictionary<string, string>()
            {
                ["ImportDirectoryBuildProps"] = "true",
                ["AlternateCommonProps"] = Path.Combine(propsDirectory!, "AlternateCommonProps.props"),
                ["ExecRoot"] = bazel.ExecRoot,
                ["BINDIR"] = bazel.BinDir,
                ["BazelExternal"] = Path.Combine(bazel.ExecRoot, "external"),
                ["RestoreConfigFile"] = nuGetConfig,
                ["PublishDir"] = PublishDir + "/",
                ["UseAppHost"] = "false", // we'll basically be making our own via the launcher
                // msbuild's shared compilation is not compatible with sandboxing because it wll delegate compilation to
                // another process that won't have access to the sandbox requesting the build.
                // ["UseSharedCompilation"] = "false",
                ["BazelBuild"] = "true",
            };

            foreach (var src in command.DirectorySrcs)
            {
                switch (src.ToLower())
                {
                    case "directory.build.props":
                        BuildEnvironment["DirectoryBuildPropsPath"] = Path.GetFullPath(src);
                        break;
                    case "directory.build.targets":
                        BuildEnvironment["ImportDirectoryBuildTargets"] = "true";
                        BuildEnvironment["DirectoryBuildTargetsPath"] = Path.GetFullPath(src);
                        break;
                }
            }

            switch (command.Action)
            {
                case "restore":
                    Targets = new[] {"Restore"};
                    // this is auto-set by NuGet.targets in Restore when restoring a referenced project. If we don't set it
                    // ahead of time, there will be a cache miss on the restored project.
                    // https://github.com/NuGet/NuGet.Client/blob/21e2a87537cd9655b7f6599af013d447aa058e29/src/NuGet.Core/NuGet.Build.Tasks/NuGet.targets#L69
                    GlobalProperties["ExcludeRestorePackageImports"] = "true";
                    break;
                case "build":
                    Targets = new[]
                    {
                        "Build",
                    };
                    break;
                case "publish":
                    Targets = new[] {"Publish"};
                    BuildEnvironment["NoBuild"] = "true";
                    break;
                case "pack":
                    Targets = new[] {"Pack"};
                    BuildEnvironment["NoBuild"] = "true";
                    break;
                default:
                    throw new ArgumentException($"Unknown action {command.Action}");
            }

            BuildEnvironment["NoWarn"] = noWarn;
        }

        public string PublishDir { get; set; }

        public Dictionary<string, string> GlobalProperties { get; set; }
        public string Configuration { get; }
        public Dictionary<string, string> BuildEnvironment { get; }
        public string[] Targets { get; }
        public string BaseIntermediateOutputPath { get; }
        public string RestoreDir => BaseIntermediateOutputPath;
        public string IntermediateOutputPath { get; }
        public string OutputPath { get; }
    }
}