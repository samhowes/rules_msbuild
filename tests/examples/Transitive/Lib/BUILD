load("//tests/tools/build_test:build_test.bzl", "build_test")
load("@rules_msbuild//dotnet:defs.bzl", "msbuild_library")

msbuild_library(
    name = "Lib",
    srcs = ["LibClass.cs"],
    project_file = "Lib.csproj",
    target_framework = "net6.0",
    visibility = ["//tests/examples/Transitive/Binary:__pkg__"],
    deps = [
        "//tests/examples/Transitive/TransitiveLib",
    ],
)

build_test(
    name = "Lib_test",
    expected_files = {
        "net6.0": [
            "Lib.dll",
            "Lib.pdb",
            "TransitiveLib.pdb",
            "TransitiveLib.dll",
            "TransitiveTransitiveLib.pdb",
            "TransitiveTransitiveLib.dll",
        ],
    },
)
