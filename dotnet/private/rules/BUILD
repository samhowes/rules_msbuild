load("@bazel_skylib//:bzl_library.bzl", "bzl_library")

bzl_library(
    name = "binary",
    srcs = ["binary.bzl"],
    visibility = ["//dotnet:__subpackages__"],
    deps = [":common.bzl"],
)

bzl_library(
    name = "library",
    srcs = ["library.bzl"],
    visibility = ["//dotnet:__subpackages__"],
    deps = [":common.bzl"],
)

bzl_library(
    name = "common",
    srcs = ["common.bzl"],
    visibility = ["//dotnet:__subpackages__"],
    deps = [],
)

exports_files(["compile.tpl.proj"])

exports_files(["restore.tpl.proj"])

exports_files(["nuget_fetch.tpl.proj"])
