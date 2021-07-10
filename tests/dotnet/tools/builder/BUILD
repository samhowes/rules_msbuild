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

msbuild_test(
    name = "BuilderTests",
    srcs = [
        "BuildCacheTests.cs",
        "BuilderTests.cs",
        "PathMapperTests.cs",
    ],
    target_framework = "netcoreapp3.1",
    deps = [
        "//dotnet/tools/Runfiles",
        "//dotnet/tools/builder:Builder",
        "@nuget//FluentAssertions",
        "@nuget//Microsoft.Build.Tasks.Core",
        "@nuget//Microsoft.NET.Test.Sdk",
        "@nuget//Moq",
        "@nuget//SamHowes.Microsoft.Build",
        "@nuget//Shouldly",
        "@nuget//xunit",
        "@nuget//xunit.runner.visualstudio",
    ],
)
