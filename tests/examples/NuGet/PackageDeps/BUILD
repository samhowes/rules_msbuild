load("//dotnet:defs.bzl", "msbuild_binary")
load("//tests/tools/build_test:build_test.bzl", "build_test")

msbuild_binary(
    name = "PackageDeps",
    srcs = ["HelloTransitiveNuget.cs"],
    project_file = "PackageDeps.csproj",
    target_framework = "net6.0",
    visibility = ["//visibility:public"],
    deps = [
        "@nuget//xunit",
    ],
)

build_test(
    name = "PackageDeps_test",
    expected_files = {
        "net6.0": [
            "PackageDeps.dll",
            # @nuget//xunit references other packages that contain these dlls
            "xunit.assert.dll",
            "xunit.abstractions.dll",
            "xunit.core.dll",
            "xunit.execution.dotnet.dll",
        ],
    },
    expected_output = "hello\n",
)
