load("@bazel_skylib//:bzl_library.bzl", "bzl_library")

package(default_visibility = ["//dotnet:__subpackages__"])

bzl_library(
    name = "bzl",
    srcs = [
        "@rules_dotnet_runtime//dotnet:defs.bzl",
        "@rules_dotnet_runtime//dotnet:providers.bzl",
        "@rules_dotnet_runtime//dotnet:repository.bzl",
    ] + glob(["*.bzl"]),
    visibility = ["//visibility:public"],
    deps = [
        "//dotnet/private/msbuild:bzl",
        "//dotnet/private/util:bzl",
        "@bazel_skylib//lib:dicts",
        "@bazel_skylib//lib:paths",
    ],
)
