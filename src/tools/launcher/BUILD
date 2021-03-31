load("@bazel_tools_public//src/tools/launcher:win_rules.bzl", "win_cc_binary", "win_cc_library")

win_cc_binary(
    name = "launcher",
    srcs = ["launcher_main.cc"],
    deps = [
        ":dotnet_launcher",
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
