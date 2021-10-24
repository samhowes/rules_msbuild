load("@bazel_skylib//:bzl_library.bzl", "bzl_library")
load("//tools/stardoc:stardoc.bzl", "stardoc")
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

stardoc(
    name = "rules.md",
    header_template = "Rules.vm",
    input = "defs.rules.bzl",
    deps = [
        ":bzl",
    ],
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
