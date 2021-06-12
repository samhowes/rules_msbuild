# Simply a sanity test so I know that bazel is running and processing the workspace correctly

load("@io_bazel_rules_go//go:def.bzl", "go_binary", "go_library")

go_binary(
    name = "sanity",
    embed = [":sanity_lib"],
    visibility = ["//visibility:public"],
)

go_library(
    name = "library",
    srcs = ["library.go"],
    importpath = "github.com/bazelbuild/rules_msbuild/tests/sanity",
)

go_library(
    name = "sanity_lib",
    srcs = [
        "hello.go",
        "library.go",
    ],
    importpath = "github.com/samhowes/rules_msbuild/tests/sanity",
    visibility = ["//visibility:private"],
)
