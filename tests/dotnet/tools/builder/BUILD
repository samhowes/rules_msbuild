load("//dotnet:defs.bzl", "dotnet_test")

dotnet_test(
    name = "BuilderTest",
    size = "small",
    srcs = glob(["*.cs"]),
    msbuild_properties = {"UseAppHost": "false"},
    sdk = "Microsoft.NET.Sdk",
    target_framework = "netcoreapp3.1",
    visibility = ["//visibility:public"],
    deps = [
        "//dotnet/tools/builder:Builder",  # keep
        "@nuget//FluentAssertions",
        "@nuget//Microsoft.NET.Test.Sdk",
        "@nuget//xunit",
        "@nuget//xunit.runner.visualstudio",
    ],
)
