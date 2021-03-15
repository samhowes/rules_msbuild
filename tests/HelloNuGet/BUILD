load("//tests/pytools:macros.bzl", "build_test")
load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_binary")

build_test(
    name = "HelloNuGet_test",
    args = ["<3"],
    expected_files = [
        "HelloNuGet.pdb",
        "CommandLineParser.dll",
        "CommandLineParser.xml",
    ],
    expected_output = "NuGet <3 Bazel",
)

dotnet_binary(
    name = "HelloNuGet",
    srcs = ["Program.cs"],
    target_framework = "netcoreapp3.1",
    deps = [
        "@nuget//CommandLineParser",
    ],
)
