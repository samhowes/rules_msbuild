load("@my_rules_dotnet//dotnet:defs.bzl", "msbuild_binary")

filegroup(
    name = "builder_srcs",
    srcs = [
        "BazelLogger.cs",
        "BazelMsBuildLogger.cs",
        "BuildContext.cs",
        "BuildManagerFields.cs",
        "Builder.cs",
        "Diagnostics.cs",
        "LaunchDataWriter.cs",
        "LauncherFactory.cs",
        "MsBuildCacheManager.cs",
        "OutputProcessor.cs",
        "Program.cs",
        "TargetGraph.cs",
    ],
    visibility = ["//visibility:public"],
)

exports_files(["Builder.csproj"])

msbuild_binary(
    name = "Builder",
    srcs = glob(["*.cs"]),
    target_framework = "netcoreapp3.1",
    visibility = ["//tests:__subpackages__"],
    deps = [
        "@nuget//Microsoft.Build",
        "@nuget//Microsoft.Build.Locator",
        "@nuget//Microsoft.Build.Utilities.Core",
    ],
)
