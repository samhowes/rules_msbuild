load("@bazel_skylib//:bzl_library.bzl", "bzl_library")

package(default_visibility = ["//dotnet:__subpackages__"])

bzl_library(
    name = "bzl",
    srcs = glob(["*.bzl"]),
    visibility = ["//visibility:public"],
    deps = [
        "//dotnet/private/msbuild:bzl",
        "@bazel_skylib//lib:dicts",
        "@bazel_skylib//lib:paths",
    ],
)
