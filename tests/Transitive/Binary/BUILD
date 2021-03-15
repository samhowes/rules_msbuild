load("//tests/pytools:macros.bzl", "build_test")
load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_binary")

build_test(
    name = "Binary_test",
    expected_files = [
        "Binary.pdb",
        "Lib.dll",
        "Lib.pdb",
        "TransitiveLib.dll",
        "TransitiveLib.pdb",
        "TransitiveTransitiveLib.dll",
        "TransitiveTransitiveLib.pdb",
    ],
    expected_output = "Hello Transitive: Transitive Transitive Value!",
)

dotnet_binary(
    name = "Binary",
    srcs = ["Program.cs"],
    target_framework = "netcoreapp3.1",
    deps = [
        "//tests/Transitive/Lib",
    ],
)
