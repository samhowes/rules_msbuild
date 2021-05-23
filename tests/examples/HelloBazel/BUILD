load("//tests/tools/build_test:build_test.bzl", "build_test")
load("@my_rules_dotnet//dotnet:defs.bzl", "msbuild_binary")

msbuild_binary(
    name = "HelloBazel",
    srcs = ["Program.cs"],
    project_file = "HelloBazel.csproj",
    target_framework = "netcoreapp3.1",
)

build_test(
    name = "HelloBazel_restore_test",
    expected_files = {
        "": [
            "HelloBazel.csproj",
            "HelloBazel_restore.binlog",
        ],
        "obj": [
            "project.assets.json",
            "HelloBazel.csproj.nuget.dgspec.json",
            "HelloBazel.csproj.nuget.g.props",
            "HelloBazel.csproj.nuget.g.targets",
        ],
    },
    target = "HelloBazel_restore",
)

build_test(
    name = "HelloBazel_test",
    expected_files = {
        "": [
            "HelloBazel.csproj.build.cache",
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
