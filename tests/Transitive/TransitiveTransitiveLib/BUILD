load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_library")

dotnet_library(
    name = "TransitiveTransitiveLib",
    target_framework = "netstandard2.1",
    srcs = ["TransitiveTransitiveLibClass.cs"],
    visibility = ["//tests/Transitive/TransitiveLib:__pkg__"],
)
