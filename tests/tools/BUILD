load("@io_bazel_rules_go//go:def.bzl", "go_test")

exports_files([
    "build_test.go",
])

go_test(
    name = "tools_test",
    srcs = ["build_test.go"],
    # this test won't pass, but I'l leave it here because gazelle generates it
    # and it is useful for debugging, hence the manual tag
    tags = ["manual"],
    deps = [
        "//tests/tools/lib",
        "@io_bazel_rules_go//go/tools/bazel:go_default_library",
    ],
)
