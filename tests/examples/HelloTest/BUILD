load("//dotnet:defs.bzl", "msbuild_test")
load("//tests/tools/build_test:build_test.bzl", "build_test")

msbuild_test(
    name = "HelloTest",
    size = "small",
    srcs = ["HelloTest.cs"],
    project_file = "HelloTest.csproj",
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
            # implicitly copied by the builder
            "Microsoft.VisualStudio.TestPlatform.Extension.JUnit.Xml.TestAdapter.dll",
            "Microsoft.VisualStudio.TestPlatform.Extension.JUnit.Xml.TestLogger.dll",
        ],
        "netcoreapp3.1/cs": [
            "Microsoft.TestPlatform.CommunicationUtilities.resources.dll",
        ],
    },
    expected_output = "%Passed:\\s+1",
    target = "HelloTest",
)
