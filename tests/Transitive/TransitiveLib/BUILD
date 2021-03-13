load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_library")

dotnet_library(
    name = "TransitiveLib",
    srcs = ["TransitiveLibClass.cs"],
    target_framework = "netstandard2.1",
    visibility = ["//tests/Transitive/Lib:__pkg__"],
    deps = [
        "//tests/Transitive/TransitiveTransitiveLib",
    ],
)
