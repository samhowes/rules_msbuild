load("@io_bazel_rules_go//go:def.bzl", "go_binary", "go_library", "go_test")

package(default_visibility = ["//visibility:public"])

filegroup(
    name = "launcher",
    srcs = select({
        "@bazel_tools//src/conditions:host_windows": [":launcher_windows"],
        # The go launcher would be completely cross platform, but we don't want to force the go toolchain on
        # anyone that doesn't want it. We'll precompile the windows exe, because there is only one os_arch combination
        # we care about. Unix all has bash though, and that works without requiring an extra toolchain, and without
        # compiling a bunch of combinations of os_arch
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

go_library(
    name = "launcher_lib",
    srcs = [
        "data_parser.go",
        "dotnet_launcher.go",
        "launcher_main.go",
    ],
    importpath = "github.com/samhowes/my_rules_dotnet/dotnet/tools/launcher",
    visibility = ["//visibility:private"],
    deps = [
        "@io_bazel_rules_go//go/tools/bazel:go_default_library",
        "@org_golang_x_sys//execabs",
    ],
)

# todo(#41) precompile this into an exe and publish as an artifact
go_binary(
    name = "launcher_windows",
    embed = [":launcher_lib"],
    visibility = ["//visibility:public"],
)

go_test(
    name = "launcher_test",
    size = "small",
    srcs = ["data_parser_test.go"],
    embed = [":launcher_lib"],
    deps = ["@io_bazel_rules_go//go/tools/bazel:go_default_library"],
)
