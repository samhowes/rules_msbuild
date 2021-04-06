load("//tests/tools:build_test.bzl", "binary_test")
load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_binary")

dotnet_binary(
    name = "Binary",
    srcs = ["Program.cs"],
    target_framework = "netcoreapp3.1",
    deps = [
        "//tests/examples/Transitive/Lib",
    ],
)

binary_test(
    name = "Binary_test",
    expected_files = {
        "netcoreapp3.1": [
            "Binary.pdb",
            "Lib.dll",
            "Lib.pdb",
            "TransitiveLib.dll",
            "TransitiveLib.pdb",
            "TransitiveTransitiveLib.dll",
            "TransitiveTransitiveLib.pdb",
        ],
    },
    expected_output = "Hello Transitive: Transitive Transitive Value!",
    target = "Binary",
)
