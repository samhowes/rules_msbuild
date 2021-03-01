load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_binary")

dotnet_binary(
    name = "Dependent",
    target_framework = "netcoreapp3.1",
    srcs = glob(["**/*.cs"]),
    deps = [
        "//tests/ClassLibrary"
    ]
)