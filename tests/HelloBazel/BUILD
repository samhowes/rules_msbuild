load("//tests/pytools:macros.bzl", "py_build_test")
load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_binary")

py_build_test(target = "HelloBazel")

dotnet_binary(
    name = "HelloBazel",
    srcs = ["Program.cs"],
    target_framework = "netcoreapp3.1",
)
