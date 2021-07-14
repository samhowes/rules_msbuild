load("@rules_msbuild//dotnet:defs.bzl", "msbuild_binary")

filegroup(
    name = "builder_srcs",
    srcs = [
        "BazelLogger.cs",
        "BuildCache.cs",
        "BuildContext.cs",
        "Builder.cs",
        "CachePlugin.cs",
        "Diagnostics/Debugger.cs",
        "Diagnostics/TargetGraph.cs",
        "Diagnostics/GraphViz/DotWriter.cs",
        "Diagnostics/GraphViz/IStyler.cs",
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
    target_framework = "netcoreapp3.1",
    visibility = ["//:__subpackages__"],
    deps = [
        "@nuget//Microsoft.Build.Locator",
        "@nuget//Microsoft.Build.Utilities.Core",
        "@nuget//SamHowes.Microsoft.Build",
    ],
)
