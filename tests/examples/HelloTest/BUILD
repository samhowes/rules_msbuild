load("//dotnet:defs.bzl", "msbuild_test")
load("//tests/tools/build_test:build_test.bzl", "build_test")

msbuild_test(
    name = "HelloTest",
    size = "small",
    srcs = ["HelloTest.cs"],
    project_file = "HelloTest.csproj",
    target_framework = "net6.0",
    deps = [
        "@nuget//Microsoft.NET.Test.Sdk",
        "@nuget//xunit",
        "@nuget//xunit.runner.visualstudio",
    ],
)

build_test(
    name = "HelloTest_test",
    expected_files = {
        "net6.0": [
            "HelloTest.dll",
            "xunit.assert.dll",
            "xunit.runner.visualstudio.dotnetcore.testadapter.dll",
            # implicitly copied by the builder
            "Microsoft.VisualStudio.TestPlatform.Extension.JUnit.Xml.TestAdapter.dll",
            "Microsoft.VisualStudio.TestPlatform.Extension.JUnit.Xml.TestLogger.dll",
        ],
        "net6.0/cs": [
            "Microsoft.TestPlatform.CommunicationUtilities.resources.dll",
        ],
    },
    expected_output = "%Passed:\\s+1",
)
