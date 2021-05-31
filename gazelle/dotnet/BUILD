load("@io_bazel_rules_go//go:def.bzl", "go_library", "go_test")
load("@bazel_gazelle//:def.bzl", "gazelle", "gazelle_binary")

# gazelle:exclude testdata

go_library(
    name = "dotnet",
    srcs = [
        "configure.go",
        "gazelle.go",
        "generate.go",
        "resolve.go",
        "update-repos.go",
    ],
    importpath = "github.com/samhowes/my_rules_dotnet/gazelle/dotnet",
    visibility = ["//visibility:public"],
    deps = [
        "//gazelle/dotnet/project",
        "//gazelle/dotnet/util",
        "@bazel_gazelle//config:go_default_library",
        "@bazel_gazelle//flag:go_default_library",
        "@bazel_gazelle//label:go_default_library",
        "@bazel_gazelle//language:go_default_library",
        "@bazel_gazelle//merger:go_default_library",
        "@bazel_gazelle//repo:go_default_library",
        "@bazel_gazelle//resolve:go_default_library",
        "@bazel_gazelle//rule:go_default_library",
        "@com_github_bazelbuild_buildtools//build:go_default_library",  # keep
    ],
)

go_test(
    name = "dotnet_test",
    size = "small",
    srcs = ["gazelle_test.go"],
    data = [
        ":gazelle-dotnet",
    ] + glob([
        "testdata/**",
    ]),
    embed = [":dotnet"],
    deps = [
        "@bazel_gazelle//testtools:go_default_library",
        "@io_bazel_rules_go//go/tools/bazel:go_default_library",
    ],
)

# This gazelle binary is used exclusively for testing the gazelle language
# extension and thus only has the dotnet language installed.
gazelle_binary(
    name = "gazelle-dotnet",
    languages = [":dotnet"],
    visibility = [
        "//:__pkg__",
    ],
)

gazelle(
    name = "gazelle",
    gazelle = ":gazelle-dotnet",
)
