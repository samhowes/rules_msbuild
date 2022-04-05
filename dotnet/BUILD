load("@bazel_skylib//:bzl_library.bzl", "bzl_library")
load("//dotnet:toolchain.bzl", "msbuild_toolchain")

package(default_visibility = ["//visibility:public"])

toolchain_type(
    name = "toolchain_type",
    visibility = ["//visibility:public"],
)

msbuild_toolchain(
    name = "toolchain_impl",
    builder = "//dotnet/tools/builder:Builder",
    dotnet_runtime = "@dotnet_runtime//:dotnet_runtime",
)

toolchain(
    name = "toolchain",
    toolchain = ":toolchain_impl",
    toolchain_type = ":toolchain_type",
)

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
    ],
)
