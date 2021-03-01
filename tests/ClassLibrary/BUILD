load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_library")

package(default_visibility = ["//tests:__subpackages__"])

dotnet_library(
    name = "ClassLibrary",
    target_framework = "netstandard2.1",
    srcs = glob(["**/*.cs"])
)
