load("//tests/tools/build_test:build_test.bzl", "build_test")
load("//dotnet:defs.bzl", "msbuild_binary")

msbuild_binary(
    name = "HelloNuGet",
    srcs = ["Program.cs"],
    project_file = "HelloNuGet.csproj",
    target_framework = "net6.0",
    visibility = ["//visibility:public"],
    deps = [
        "@nuget//CommandLineParser",
    ],
)

build_test(
    name = "HelloNuGet_test",
    args = ["<3"],
    expected_files = {
        "net6.0": [
            "HelloNuGet.dll",
            "CommandLine.dll",
        ],
    },
    expected_output = "NuGet <3 Bazel\n",
)
