load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_library")

dotnet_library(
    name = "Lib",
    target_framework = "netstandard2.1",
    srcs = ["LibClass.cs"],
    visibility = ["//tests/Transitive:__subpackages__"],
    deps = [
        "//tests/Transitive/TransitiveLib"
    ]
)
