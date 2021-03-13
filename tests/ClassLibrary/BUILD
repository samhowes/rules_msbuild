load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_library")

package(default_visibility = ["//tests:__subpackages__"])

dotnet_library(
    name = "ClassLibrary",
    srcs = glob(["**/*.cs"]),
    target_framework = "netstandard2.1",
)
