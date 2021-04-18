load("//dotnet:defs.bzl", "dotnet_binary")
load("//tests/tools/build_test:build_test.bzl", "build_test")

dotnet_binary(
    name = "PackageDeps",
    srcs = ["HelloTransitiveNuget.cs"],
    target_framework = "netcoreapp3.1",
    deps = [
        "@nuget//xunit",
    ],
)

build_test(
    name = "PackageDeps_test",
    expected_files = {
        "netcoreapp3.1": [
            "PackageDeps.dll",
            "xunit.assert.dll",
            "xunit.abstractions.dll",
            "xunit.core.dll",
            "xunit.execution.dotnet.dll",
        ],
    },
    expected_output = "hello\n",
    target = "PackageDeps",
)
