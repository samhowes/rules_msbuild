load("@rules_msbuild//dotnet:defs.bzl", "msbuild_binary")

msbuild_binary(
    name = "NuGetParser",
    srcs = glob(["*.cs"]),
    target_framework = "netcoreapp3.1",  # keep
    visibility = ["//visibility:public"],
)

filegroup(
    name = "files",
    srcs = glob(["*"]),
)
