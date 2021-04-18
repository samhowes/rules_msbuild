load("//tests/tools/build_test:build_test.bzl", "build_test")
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

build_test(
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
