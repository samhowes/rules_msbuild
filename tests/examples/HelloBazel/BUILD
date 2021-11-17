load("//tests/tools/build_test:build_test.bzl", "build_test")
load("//dotnet:defs.bzl", "msbuild_binary")

msbuild_binary(
    name = "HelloBazel",
    srcs = ["Program.cs"],
    packable = True,
    project_file = "HelloBazel.csproj",
    target_framework = "net6.0",
    version = "1.2.3",
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
            "HelloBazel.csproj.bazel.props",
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
            # binlog & dot is only output with --define=BUILD_DIAG=1
            "HelloBazel.binlog",
            "HelloBazel.dot",
        ],
        "obj/net6.0": [
            "HelloBazel.dll",
        ],
        "net6.0": [
            # pdb is only output with compilation mode=dbg
            "HelloBazel.pdb",
            "HelloBazel.dll",
            "HelloBazel.deps.json",
            "HelloBazel.runtimeconfig.json",
        ],
    },
    expected_output = "Hello Bazel!\n",
)

build_test(
    name = "HelloBazel_publish_test",
    expected_files = {
        "publish/net6.0": [
            "HelloBazel",
            "HelloBazel.exe",
            "HelloBazel.pdb",
            "HelloBazel.dll",
            "HelloBazel.deps.json",
            "HelloBazel.runtimeconfig.json",
        ],
    },
    expected_output = "Hello Bazel!\n",
)

build_test(
    name = "HelloBazel.nupkg_test",
    expected_files = {
        "": [
            "HelloBazel.1.2.3.nupkg",
        ],
    },
)
