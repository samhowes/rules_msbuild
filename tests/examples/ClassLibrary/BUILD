load("//tests/tools/build_test:build_test.bzl", "build_test")
load("@my_rules_dotnet//dotnet:defs.bzl", "msbuild_library")

package(default_visibility = ["//tests:__subpackages__"])

msbuild_library(
    name = "ClassLibrary",
    srcs = glob(["**/*.cs"]),
    project_file = "ClassLibrary.csproj",
    target_framework = "netstandard2.1",
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
    target = "ClassLibrary_restore",
)

build_test(
    name = "ClassLibrary_test",
    expected_files = {
        "": [
            "ClassLibrary.cache",
            "ClassLibrary.binlog",
        ],
        "obj/netstandard2.1": [
            "ClassLibrary.dll",
        ],
        "netstandard2.1": [
            "ClassLibrary.pdb",
            "ClassLibrary.dll",
            "ClassLibrary.deps.json",
        ],
    },
    target = "ClassLibrary",
)
