load("//tests/pytools:macros.bzl", "py_build_test")
load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_binary")

py_build_test(target = "Dependent")

dotnet_binary(
    name = "Dependent",
    srcs = glob(["**/*.cs"]),
    target_framework = "netcoreapp3.1",
    deps = [
        "//tests/ClassLibrary",
    ],
)
