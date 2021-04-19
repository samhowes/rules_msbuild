load("@io_bazel_rules_go//go:def.bzl", "go_library")

go_library(
    name = "executable",
    srcs = ["check_executable.go"],
    importpath = "github.com/samhowes/my_rules_dotnet/tests/tools/executable",
    visibility = ["//visibility:public"],
    deps = [
        "//tests/tools/files",
        "@com_github_stretchr_testify//assert",
        "@com_github_termie_go_shutil//:go-shutil",
        "@io_bazel_rules_go//go/tools/bazel:go_default_library",
        "@org_golang_x_sys//execabs",
    ],
)
