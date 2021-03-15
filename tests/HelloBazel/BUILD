load("//tests/pytools:macros.bzl", "build_test")
load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_binary")

build_test(
    name = "HelloBazel_test",
    expected_files = [
        "HelloBazel.pdb",
    ],
    expected_output = "Hello Bazel!",
)

dotnet_binary(
    name = "HelloBazel",
    srcs = ["Program.cs"],
    target_framework = "netcoreapp3.1",
)
