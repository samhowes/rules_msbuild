load("//tests/tools:build_test.bzl", "library_test")
load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_library")

package(default_visibility = ["//tests:__subpackages__"])

dotnet_library(
    name = "ClassLibrary",
    srcs = glob(["**/*.cs"]),
    target_framework = "netstandard2.1",
)

library_test(
    name = "ClassLibrary_test",
    expected_files = {
        "netstandard2.1": [
            "ClassLibrary.dll",
            "ClassLibrary.pdb",
        ],
    },
    target = "ClassLibrary",
)
