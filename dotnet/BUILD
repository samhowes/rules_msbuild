load("@bazel_skylib//:bzl_library.bzl", "bzl_library")

toolchain_type(
    name = "toolchain",
    visibility = ["//visibility:public"],
)

bzl_library(
    name = "deps",
    srcs = ["deps.bzl"],
    visibility = ["//visibility:public"],
    deps = [
        "//dotnet/private:repositories",
        "//dotnet/private:sdk",
    ],
)
