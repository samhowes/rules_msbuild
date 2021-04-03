load("@bazel_tools_public//src/tools/launcher:win_rules.bzl", "win_cc_binary", "win_cc_library")

package(default_visibility = ["//visibility:public"])

filegroup(
    name = "launcher",
    srcs = select({
        "@bazel_tools//src/conditions:host_windows": [":launcher_windows"],
        "//conditions:default": [
            ":launcher_unix",
        ],
    }),
)

filegroup(
    name = "launcher_unix",
    srcs = ["unix.template.sh"],
    data = ["@bazel_tools//tools/bash/runfiles"],
)

# todo(#41) precompile this into an exe and publish as an artifact
win_cc_binary(
    name = "launcher_windows",
    srcs = ["launcher_main.cc"],
    visibility = ["//visibility:public"],
    deps = [
        ":dotnet_launcher",
        "@bazel_tools//tools/cpp/runfiles",
        "@bazel_tools_public//src/tools/launcher:launcher_base",
        "@bazel_tools_public//src/tools/launcher/util",
        "@bazel_tools_public//src/tools/launcher/util:data_parser",
    ],
)

win_cc_library(
    name = "dotnet_launcher",
    srcs = ["dotnet_launcher.cc"],
    hdrs = ["dotnet_launcher.h"],
    deps = [
        "@bazel_tools_public//src/tools/launcher:launcher_base",
    ],
)
