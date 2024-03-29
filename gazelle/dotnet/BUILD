load("@io_bazel_rules_go//go:def.bzl", "go_library", "go_test")
load("@bazel_gazelle//:def.bzl", "gazelle", "gazelle_binary")

# gazelle:exclude testdata
# gazelle:go_naming_convention_external go_default_library

go_library(
    name = "dotnet",
    srcs = [
        "configure.go",
        "gazelle.go",
        "generate.go",
        "resolve.go",
        "update-repos.go",
    ],
    importpath = "github.com/samhowes/rules_msbuild/gazelle/dotnet",
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

gazelle_binary(
    name = "gazelle-dotnet",
    languages = [
        "@bazel_gazelle//language/proto:go_default_library",
        ":dotnet",
    ],
    visibility = [
        "//:__subpackages__",
    ],
)

gazelle(
    name = "gazelle",
    gazelle = ":gazelle-dotnet",
)
