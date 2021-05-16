load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_binary", "dotnet_publish")
load("//dotnet:defs.bzl", "dotnet_tool_binary")

filegroup(
    name = "builder_srcs",
    srcs = [
        "BazelLogger.cs",
        "BazelMsBuildLogger.cs",
        "BuildManagerFields.cs",
        "Builder.cs",
        "LaunchDataWriter.cs",
        "LauncherFactory.cs",
        "MsBuildCacheManager.cs",
        "OutputProcessor.cs",
        "ProcessorContext.cs",
        "Program.cs",
    ],
    visibility = ["//visibility:public"],
)

exports_files(["Builder.csproj"])

dotnet_binary(
    name = "Builder",
    srcs = glob(["*.cs"]),
    msbuild_properties = {"DisableMSBuildAssemblyCopyCheck":"true"},
    sdk = "Microsoft.NET.Sdk",
    target_framework = "netcoreapp3.1",
    visibility = ["//visibility:public"],
    deps = [
        "@nuget//Microsoft.Build",
        "@nuget//Microsoft.Build.Locator",
        "@nuget//Microsoft.Build.Utilities.Core",
    ],
)

dotnet_publish(
    name = "publish",
    target = ":Builder",
)
