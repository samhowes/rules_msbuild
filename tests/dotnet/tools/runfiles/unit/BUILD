load("//dotnet:defs.bzl", "msbuild_test")

msbuild_test(
    name = "RunfilesTests",
    size = "small",
    srcs = [
        "LabelTests.cs",
        "MockFile.cs",
        "RunfilesTest.cs",
    ],
    target_framework = "netcoreapp3.1",
    deps = [
        "//dotnet/tools/Runfiles",
        "@nuget//FluentAssertions",
        "@nuget//Microsoft.NET.Test.Sdk",
        "@nuget//xunit",
        "@nuget//xunit.runner.visualstudio",
    ],
)
