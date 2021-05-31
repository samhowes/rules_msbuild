load("//dotnet:defs.bzl", "msbuild_test")

msbuild_test(
    name = "RunfilesTests",
    size = "small",
    srcs = glob(["*.cs"]),
    target_framework = "netcoreapp3.1",
    deps = [
        "//dotnet/tools/runfiles",  # keep
        "@nuget//FluentAssertions",
        "@nuget//Microsoft.NET.Test.Sdk",
        "@nuget//xunit",
        "@nuget//xunit.runner.visualstudio",
    ],
)
