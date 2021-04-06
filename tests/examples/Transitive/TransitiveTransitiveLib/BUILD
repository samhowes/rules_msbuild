load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_library")

dotnet_library(
    name = "TransitiveTransitiveLib",
    srcs = ["TransitiveTransitiveLibClass.cs"],
    target_framework = "netstandard2.1",
    visibility = ["//tests/examples/Transitive/TransitiveLib:__pkg__"],
)
