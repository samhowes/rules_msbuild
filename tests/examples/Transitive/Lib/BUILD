load("//tests/tools:build_test.bzl", "library_test")
load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_library")

dotnet_library(
    name = "Lib",
    srcs = ["LibClass.cs"],
    target_framework = "netstandard2.1",
    visibility = ["//tests/examples/Transitive/Binary:__pkg__"],
    deps = [
        "//tests/examples/Transitive/TransitiveLib",
    ],
)

library_test(
    name = "Lib_test",
    expected_files = {
        "netstandard2.1": [
            "Lib.dll",
            "Lib.pdb",
            "TransitiveLib.pdb",
            "TransitiveLib.dll",
            "TransitiveTransitiveLib.pdb",
            "TransitiveTransitiveLib.dll",
        ],
    },
    target = "Lib",
)
