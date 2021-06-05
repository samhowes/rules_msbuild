load("//tests/tools/build_test:build_test.bzl", "build_test")
load("//dotnet:defs.bzl", "msbuild_binary")

msbuild_binary(
    name = "HelloBazel",
    srcs = glob(["Program.cs"]),
    project_file = "HelloBazel.csproj",
    target_framework = "netcoreapp3.1",
    visibility = ["//visibility:public"],
)

build_test(
    name = "HelloBazel_restore_test",
    expected_files = {
        "": [
            "HelloBazel_restore.binlog",
        ],
        "restore": [
            "project.assets.json",
            "HelloBazel.csproj.nuget.dgspec.json",
            "HelloBazel.csproj.nuget.g.props",
            "HelloBazel.csproj.nuget.g.targets",
        ],
    },
)

build_test(
    name = "HelloBazel_test",
    expected_files = {
        "": [
            "HelloBazel.cache",
            # binlog is only output with --define=BUILD_DIAG=1
            "HelloBazel.binlog",
        ],
        "obj/netcoreapp3.1": [
            "HelloBazel.dll",
        ],
        "netcoreapp3.1": [
            # pdb is only output with compilation mode=dbg
            "HelloBazel.pdb",
            "HelloBazel.dll",
            "HelloBazel.deps.json",
            "HelloBazel.runtimeconfig.json",
            "HelloBazel.runtimeconfig.dev.json",
        ],
    },
    expected_output = "Hello Bazel!\n",
)

build_test(
    name = "HelloBazel_publish_test",
    expected_files = {
        "publish/netcoreapp3.1": [
            "HelloBazel.pdb",
            "HelloBazel.dll",
            "HelloBazel.deps.json",
            "HelloBazel.runtimeconfig.json",
        ],
    },
)
