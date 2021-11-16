load("//tests/tools/build_test:build_test.bzl", "build_test")
load("@rules_msbuild//dotnet:defs.bzl", "msbuild_binary")

msbuild_binary(
    name = "Binary",
    srcs = ["Program.cs"],
    project_file = "Binary.csproj",
    target_framework = "net6.0",
    visibility = ["//visibility:public"],
    deps = [
        "//tests/examples/Transitive/Lib",
    ],
)

build_test(
    name = "Binary_test",
    expected_files = {
        "net6.0": [
            "Binary.dll",
            "Binary.pdb",
            "Lib.dll",
            "Lib.pdb",
            "TransitiveLib.dll",
            "TransitiveLib.pdb",
            "TransitiveTransitiveLib.dll",
            "TransitiveTransitiveLib.pdb",
        ],
    },
    expected_output = "Hello Transitive: Transitive Transitive Value!\n",
)
