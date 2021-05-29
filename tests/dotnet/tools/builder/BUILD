load("//dotnet:defs.bzl", "msbuild_test")

msbuild_test(
    name = "BuilderTest",
    size = "small",
    srcs = glob(["*.cs"]),
    project_file = "BuilderTest.csproj",
    target_framework = "netcoreapp3.1",
    deps = [
        "//dotnet/tools/builder:Builder",  # keep
        "@nuget//FluentAssertions",
        "@nuget//Microsoft.NET.Test.Sdk",
        "@nuget//xunit",
        "@nuget//xunit.runner.visualstudio",
    ],
)
