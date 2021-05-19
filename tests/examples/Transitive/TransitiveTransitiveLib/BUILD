load("//tests/tools/build_test:build_test.bzl", "build_test")
load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_library")

dotnet_library(
    name = "TransitiveTransitiveLib",
    srcs = ["TransitiveTransitiveLibClass.cs"],
    target_framework = "netstandard2.1",
    visibility = ["//tests/examples/Transitive/TransitiveLib:__pkg__"],
)

build_test(
    name = "TransitiveTransitiveLib_test",
    expected_files = {
        "": [
            "TransitiveTransitiveLib.csproj.build.cache",
        ],
        "netstandard2.1": [
            "TransitiveTransitiveLib.pdb",
            "TransitiveTransitiveLib.dll",
        ],
    },
    target = "TransitiveTransitiveLib",
)
