load("//dotnet:defs.bzl", "dotnet_test")
load("//tests/tools:build_test.bzl", "build_test")

dotnet_test(
    name = "HelloTest",
    size = "small",
    srcs = ["HelloTest.cs"],
    target_framework = "netcoreapp3.1",
    deps = [
        "@nuget//Microsoft.NET.Test.Sdk",
        "@nuget//xunit",
        "@nuget//xunit.runner.visualstudio",
    ],
)

build_test(
    name = "HelloTest_test",
    expected_files = {
        "netcoreapp3.1": [
            "HelloTest.dll",
            "xunit.assert.dll",
            "xunit.runner.visualstudio.dotnetcore.testadapter.dll",
        ],
        "netcoreapp3.1/cs": [
            "Microsoft.TestPlatform.CommunicationUtilities.resources.dll",
        ],
    },
    expected_output = "%Total tests: 1\n\\s+Passed: 1",
    target = "HelloTest",
)
