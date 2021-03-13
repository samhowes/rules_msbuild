load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_binary")

dotnet_binary(
    name = "Binary",
    srcs = ["Program.cs"],
    target_framework = "netcoreapp3.1",
    deps = [
        "//tests/Transitive/Lib",
    ],
)
