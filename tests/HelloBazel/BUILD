load("@rules_python//python:defs.bzl", "py_test")
load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_binary")

py_test(
    name = "HelloBazel_test",
    srcs = ["HelloBazel_test.py"],
    args = [
        "$(rootpath :HelloBazel)",
    ],
    data = [":HelloBazel"],
    deps = [
        "@rules_python//python/runfiles",
    ],
)

dotnet_binary(
    name = "HelloBazel",
    srcs = ["Program.cs"],
    target_framework = "netcoreapp3.1",
)
