load("@bazel_skylib//:bzl_library.bzl", "bzl_library")
load(":toolchains.bzl", "declare_constraints")

package(default_visibility = ["//visibility:public"])

toolchain_type(
    name = "toolchain",
    visibility = ["//visibility:public"],
)

declare_constraints()

exports_files(
    ["defs.bzl"],
    visibility = ["//docs:__subpackages__"],
)

bzl_library(
    name = "bzl",
    srcs = glob(["*.bzl"]),
    deps = [
        "//dotnet/private:bzl",
        "//dotnet/private/rules:bzl",
        "//dotnet/private/toolchain:bzl",
    ],
)
