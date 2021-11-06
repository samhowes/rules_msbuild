load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")
load("@bazel_tools//tools/build_defs/repo:git.bzl", "git_repository")
load("@bazel_tools//tools/build_defs/repo:utils.bzl", "maybe")

def msbuild_rules_dependencies():
    maybe(
        http_archive,
        name = "platforms",
        strip_prefix = "platforms-0.0.1",
        # 0.0.1, latest as of 2020-12-01
        urls = [
            "https://mirror.bazel.build/github.com/bazelbuild/platforms/archive/0.0.1.zip",
            "https://github.com/bazelbuild/platforms/archive/0.0.1.zip",
        ],
        sha256 = "2bf34f026351d4b4b46b17956aa5b977cc1279d5679385f6885bf574dec5570c",
    )

    maybe(
        http_archive,
        name = "bazel_skylib",
        # 1.0.3, latest as of 2020-12-01
        urls = [
            "https://mirror.bazel.build/github.com/bazelbuild/bazel-skylib/releases/download/1.0.3/bazel-skylib-1.0.3.tar.gz",
            "https://github.com/bazelbuild/bazel-skylib/releases/download/1.0.3/bazel-skylib-1.0.3.tar.gz",
        ],
        sha256 = "1c531376ac7e5a180e0237938a2536de0c54d93f5c278634818e0efc952dd56c",
    )
    maybe(
        git_repository,
        name = "rules_dotnet_runtime",
        commit = "3480356f4ab70015b99207d7a724ca1d24323093",  # branch main as of 2021-11-06
        shallow_since = "1636220163 -0400",
        remote = "https://github.com/samhowes/rules_dotnet_runtime",
    )
