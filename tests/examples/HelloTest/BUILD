load("//dotnet:defs.bzl", "dotnet_test")
load("//tests/tools:build_test.bzl", "binary_test")

dotnet_test(
    name = "HelloTest",
    srcs = ["HelloTest.cs"],
    target_framework = "netcoreapp3.1",
    deps = [
        "@nuget//Microsoft.NET.Test.Sdk",
        "@nuget//xunit",
        "@nuget//xunit.runner.visualstudio",
    ],
)

binary_test(
    name = "HelloTest_test",
    expected_files = {
        "netcoreapp3.1": [
            "HelloTest_test.dll",
            "xunit.assert.dll",
            "xunit.runner.visualstudio.dotnetcore.testadapter.dll",
        ],
    },
    target = "HelloTest",
)
