load("@bazel_skylib//:bzl_library.bzl", "bzl_library")

# gazelle:exclude *.proj

exports_files([
    "NuGet.tpl.config",
    "project.tpl.proj",
    "AlternateCommonProps.props",
    "Directory.Bazel.props",
    "Directory.Bazel.targets",
])

bzl_library(
    name = "bzl",
    srcs = glob(["*.bzl"]),
    visibility = ["//visibility:public"],
    deps = [
    ],
)
