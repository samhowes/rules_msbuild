load("//tests/tools:build_test.bzl", "library_test")
load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_library")

dotnet_library(
    name = "TransitiveLib",
    srcs = ["TransitiveLibClass.cs"],
    target_framework = "netstandard2.1",
    visibility = ["//tests/Transitive/Lib:__pkg__"],
    deps = [
        "//tests/Transitive/TransitiveTransitiveLib",
    ],
)

library_test(
    name = "TransitiveLib_test",
    expected_files = {
        "netstandard2.1": [
            "TransitiveLib.pdb",
            "TransitiveLib.dll",
            "TransitiveTransitiveLib.pdb",
            "TransitiveTransitiveLib.dll",
        ],
    },
    target = "TransitiveLib",
)

