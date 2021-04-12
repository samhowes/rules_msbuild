load("//tests/tools:build_test.bzl", "binary_test")
load("//dotnet:defs.bzl", "dotnet_binary")

dotnet_binary(
    name = "Binary",
    srcs = ["Program.cs"],
    target_framework = "netcoreapp3.1",
    deps = [
        "//tests/examples/NuGet/Transitive/Library",
    ],
)

binary_test(
    name = "Binary_test",
    expected_files = {
        "netcoreapp3.1": [
            "Library.dll",
            "NewtonSoft.Json.dll",
        ],
    },
    expected_output = "bar",
    target = "Binary",
)
