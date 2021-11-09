load("//dotnet:defs.bzl", "msbuild_test")

msbuild_test(
    name = "BuilderTests",
    size = "small",
    srcs = [
        "BuildCacheTests.cs",
        "BuildContextTests.cs",
        "BuildFrameworkTest.cs",
        "BuilderTests.cs",
        "E2eTests.cs",
        "PathMapperTests.cs",
        "RestoreFixerTests.cs",
        "TestProgram.cs",
    ],
    dotnet_cmd = "exec",
    target_framework = "net5.0",
    test_env = {
        "BAZEL_DOTNET_SDKROOT": "$(BAZEL_DOTNET_SDKROOT)",
    },
    deps = [
        "//dotnet/tools/Runfiles",
        "//dotnet/tools/builder:Builder",
        "@nuget//FluentAssertions",
        "@nuget//Microsoft.Build.Tasks.Core",
        "@nuget//Microsoft.NET.Test.Sdk",
        "@nuget//Moq",
        "@nuget//SamHowes.Microsoft.Build",
        "@nuget//xunit.assert",
        "@nuget//xunit.console",
        "@nuget//xunit.core",
        "@nuget//xunit.extensibility.core",
    ],
)
