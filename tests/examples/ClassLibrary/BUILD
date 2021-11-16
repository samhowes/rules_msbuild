load("//tests/tools/build_test:build_test.bzl", "build_test")
load("@rules_msbuild//dotnet:defs.bzl", "msbuild_library")

package(default_visibility = ["//tests:__subpackages__"])

msbuild_library(
    name = "ClassLibrary",
    srcs = glob(["**/*.cs"]),
    project_file = "ClassLibrary.csproj",
    target_framework = "net6.0",
)

build_test(
    name = "ClassLibrary_restore_test",
    expected_files = {
        "": [
            "ClassLibrary_restore.binlog",
        ],
        "restore": [
            "project.assets.json",
            "ClassLibrary.csproj.nuget.dgspec.json",
            "ClassLibrary.csproj.nuget.g.props",
            "ClassLibrary.csproj.nuget.g.targets",
        ],
    },
)

build_test(
    name = "ClassLibrary_test",
    expected_files = {
        "": [
            "ClassLibrary.cache",
            "ClassLibrary.binlog",
        ],
        "obj/net6.0": [
            "ClassLibrary.dll",
        ],
        "net6.0": [
            "ClassLibrary.pdb",
            "ClassLibrary.dll",
            "ClassLibrary.deps.json",
        ],
    },
)
