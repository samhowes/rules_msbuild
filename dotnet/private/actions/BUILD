load("@bazel_skylib//:bzl_library.bzl", "bzl_library")

package(default_visibility = ["//dotnet:__subpackages__"])

bzl_library(
    name = "bzl",
    srcs = glob(["*.bzl"]) + ["@rules_dotnet_runtime//dotnet:defs.bzl"],
    visibility = ["//visibility:public"],
    deps = [
        "//dotnet/private/msbuild:bzl",
        "//dotnet/private/util:bzl",
        "@bazel_skylib//lib:dicts",
        "@bazel_skylib//lib:paths",
    ],
)
