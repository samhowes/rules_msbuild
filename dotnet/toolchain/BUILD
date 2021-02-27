load("@bazel_skylib//:bzl_library.bzl", "bzl_library")
load(
    ":toolchains.bzl",
    "declare_constraints",
)

package(default_visibility = ["//visibility:public"])

declare_constraints()

bzl_library(
    name = "toolchains",
    srcs = ["toolchains.bzl"],
    deps = [
        "@my_rules_dotnet//dotnet/private:platforms",
        "@my_rules_dotnet//dotnet/private:sdk",
    ],
)
