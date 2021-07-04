load("@bazel_gazelle//:deps.bzl", "go_repository")
load("@bazel_tools//tools/build_defs/repo:utils.bzl", "maybe")

def dotnet_rules_dependencies():
    maybe(
        go_repository,
        name = "org_golang_x_sys",
        importpath = "golang.org/x/sys",
        sum = "h1:6L+uOeS3OQt/f4eFHXZcTxeZrGCuz+CLElgEBjbcTA4=",
        version = "v0.0.0-20210415045647-66c3f260301c",
    )
