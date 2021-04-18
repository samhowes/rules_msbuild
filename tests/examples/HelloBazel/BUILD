load("//tests/tools:build_test.bzl", "build_test")
load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_binary")

dotnet_binary(
    name = "HelloBazel",
    srcs = ["Program.cs"],
    target_framework = "netcoreapp3.1",
)

build_test(
    name = "HelloBazel_test",
    expected_files = {
        "": [
            "HelloBazel.csproj.build.binlog",
            "HelloBazel.csproj.restore.binlog",
        ],
        "netcoreapp3.1": [
            "HelloBazel.pdb",
            "HelloBazel.dll",
            "HelloBazel.deps.json",
            "HelloBazel.runtimeconfig.json",
            "HelloBazel.runtimeconfig.dev.json",
        ],
    },
    expected_output = "Hello Bazel!\n",
    target = "HelloBazel",
)
