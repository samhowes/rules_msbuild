load("//tests/pytools:macros.bzl", "build_test")
load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_binary")

build_test(
    name = "Dependent_test",
    expected_files = [
        "Dependent.pdb",
        "ClassLibrary.dll",
        "ClassLibrary.pdb",
    ],
    expected_output = "Hello Secret: Foo",
)

dotnet_binary(
    name = "Dependent",
    srcs = glob(["**/*.cs"]),
    target_framework = "netcoreapp3.1",
    deps = [
        "//tests/ClassLibrary",
    ],
)
