load("//eng:util.bzl", "expand")

PACKAGES = [
    "//gazelle/dotnet:gazelle-dotnet",
    "//dotnet/tools/launcher",
]

sh_binary(
    name = "publish",
    srcs = ["publish.sh"],
    args = expand(PACKAGES),
    data = PACKAGES,
    deps = ["@bazel_tools//tools/bash/runfiles"],
)
