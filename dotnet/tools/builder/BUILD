load("@rules_msbuild//dotnet:defs.bzl", "msbuild_binary")

filegroup(
    name = "builder_srcs",
    srcs = [
        "BazelLogger.cs",
        "BazelPropsWriter.cs",
        "BuildContext.cs",
        "Builder.cs",
        "CachePlugin.cs",
        "Caching/BuildCache.cs",
        "Caching/BuildResult.cs",
        "Caching/CacheManifest.cs",
        "Caching/Label.cs",
        "Caching/LabelResult.cs",
        "Diagnostics/Debugger.cs",
        "Diagnostics/GraphViz/DotWriter.cs",
        "Diagnostics/GraphViz/IStyler.cs",
        "Diagnostics/TargetGraph.cs",
        "Diagnostics/TargetGraphLogger.cs",
        "Files.cs",
        "Launcher/LaunchDataWriter.cs",
        "Launcher/LauncherFactory.cs",
        "MSBuild/BazelMsBuildLogger.cs",
        "MSBuild/BuildManagerFields.cs",
        "MSBuild/MSBuildContext.cs",
        "MSBuild/MsBuildCacheManager.cs",
        "MSBuild/Serialization.cs",
        "OutputProcessor.cs",
        "PathMapper.cs",
        "Program.cs",
        "ProjectLoader.cs",
    ],
    visibility = ["//visibility:public"],
)

exports_files(["Builder.csproj"])

msbuild_binary(
    name = "Builder",
    srcs = [":builder_srcs"],
    project_file = "Builder.csproj",
    target_framework = "net5.0",
    visibility = ["//:__subpackages__"],
    deps = [
        "@nuget//Microsoft.Build.Locator",
        "@nuget//Microsoft.Build.Utilities.Core",
        "@nuget//SamHowes.Microsoft.Build",
    ],
)
