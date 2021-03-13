load("@bazel_skylib//:bzl_library.bzl", "bzl_library")

package(default_visibility = ["//dotnet:__subpackages__"])

bzl_library(
    name = "repositories",
    srcs = ["repositories.bzl"],
    deps = [],
)

bzl_library(
    name = "sdk",
    srcs = ["sdk.bzl"],
    deps = [],
)

bzl_library(
    name = "dotnet_context_data",
    srcs = ["context.bzl"],
    deps = [
        ":common.bzl",
    ],
)

bzl_library(
    name = "common",
    srcs = ["common.bzl"],
)
