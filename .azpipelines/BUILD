load(":util.bzl", "expand")

PACKAGES = [
    "//gazelle/dotnet:gazelle-dotnet",
    "//dotnet/tools/Bzl:SamHowes.Bzl_nuget",
]

sh_binary(
    name = "publish",
    srcs = ["publish.sh"],
    args = expand(PACKAGES),
    data = PACKAGES,
    deps = ["@bazel_tools//tools/bash/runfiles"],
)
