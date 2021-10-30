load("@bazel_skylib//:bzl_library.bzl", "bzl_library")

bzl_library(
    name = "bzl",
    srcs = glob(["*.bzl"]),
    visibility = ["//visibility:public"],
    deps = [
        "//:bzl",
        "//dotnet/private/actions:bzl",
        "//dotnet/private/msbuild:bzl",
        "@bazel_skylib//lib:dicts",
        "@bazel_skylib//lib:paths",
    ],
)
