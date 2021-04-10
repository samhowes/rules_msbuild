load("//tests/tools:build_test.bzl", "binary_test")
load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_binary")

dotnet_binary(
    name = "HelloNuGet",
    srcs = ["Program.cs"],
    target_framework = "netcoreapp3.1",
    deps = [
        "@nuget//CommandLineParser",
    ],
)

binary_test(
    name = "HelloNuGet_test",
    args = ["<3"],
    expected_files = {
        "netcoreapp3.1": [
            "HelloNuGet.dll",
            "CommandLine.dll",
        ],
    },
    expected_output = "NuGet <3 Bazel",
    target = "HelloNuGet",
)
