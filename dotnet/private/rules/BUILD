load("@bazel_skylib//:bzl_library.bzl", "bzl_library")
load("//tools/stardoc:stardoc.bzl", "stardoc")

bzl_library(
    name = "bzl",
    srcs = glob(["*.bzl"]),
    visibility = ["//visibility:public"],
)

stardoc(
    name = "docs",
    out = "Docs.md",
    header_template = "Docs.vm",
    input = "defs.docs.bzl",
    visibility = ["//visibility:public"],
    deps = [
        ":bzl",
        "//dotnet/private:bzl",
    ],
)
