load("//tests/tools:build_test.bzl", "binary_test")
load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_binary")

dotnet_binary(
    name = "HelloBazel",
    srcs = ["Program.cs"],
    target_framework = "netcoreapp3.1",
)

# checks the compilation output from :HelloBazel
binary_test(
    name = "HelloBazel_test",
    expected_files = {
        "": [
            "HelloBazel.csproj.binlog",
            "HelloBazel.restore.proj.binlog",
        ],
        "netcoreapp3.1": [
            "HelloBazel.pdb",
            "HelloBazel.dll",
            "HelloBazel.deps.json",
            "HelloBazel.runtimeconfig.json",
            "HelloBazel.runtimeconfig.dev.json",
        ],
        "@dotnet_sdk": [
            "sdk/3.1.100/dotnet.dll",
        ],
    },
    target = "HelloBazel",
)
