# Simply a sanity test so I know that bazel is running and processing the workspace correctly

load("@io_bazel_rules_go//go:def.bzl", "go_binary", "go_library")

go_binary(
    name = "sanity",
    srcs = ["hello.go"],
    deps = [
        ":library",
    ],
)

go_library(
    name = "library",
    srcs = ["library.go"],
    importpath = "github.com/bazelbuild/my_rules_dotnet/tests/sanity",
)
