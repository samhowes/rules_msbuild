load("//tests/tools/build_test:build_test.bzl", "build_test")
load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_binary")

dotnet_binary(
    name = "Dependent",
    srcs = glob(["**/*.cs"]),
    target_framework = "netcoreapp3.1",
    deps = [
        "//tests/examples/ClassLibrary",
    ],
)

build_test(
    name = "Dependent_test",
    expected_files = {
        "netcoreapp3.1": [
            "Dependent.dll",
            "ClassLibrary.pdb",
            "ClassLibrary.dll",
        ],
    },
    expected_output = "Hello Secret: Foo\n",
    target = "Dependent",
)
