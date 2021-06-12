load("//tests/tools/build_test:build_test.bzl", "build_test")
load("@rules_msbuild//dotnet:defs.bzl", "msbuild_library")

msbuild_library(
    name = "TransitiveLib",
    srcs = ["TransitiveLibClass.cs"],
    project_file = "TransitiveLib.csproj",
    target_framework = "netstandard2.1",
    visibility = ["//tests/examples/Transitive/Lib:__pkg__"],
    deps = [
        "//tests/examples/Transitive/TransitiveTransitiveLib",
    ],
)

build_test(
    name = "TransitiveLib_test",
    expected_files = {
        "netstandard2.1": [
            "TransitiveLib.pdb",
            "TransitiveLib.dll",
            "TransitiveTransitiveLib.pdb",
            "TransitiveTransitiveLib.dll",
        ],
    },
)
