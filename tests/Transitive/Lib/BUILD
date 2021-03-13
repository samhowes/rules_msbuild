load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_library")

dotnet_library(
    name = "Lib",
    srcs = ["LibClass.cs"],
    target_framework = "netstandard2.1",
    visibility = ["//tests/Transitive:__subpackages__"],
    deps = [
        "//tests/Transitive/TransitiveLib",
    ],
)
