"""Dependencies of dotnet_rules"""

load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")
load("@bazel_tools//tools/build_defs/repo:git.bzl", "git_repository")

def dotnet_rules_repositories():
    # Repository of standard constraint settings and values.
    # Bazel declares this automatically after 0.28.0, but it's better to
    # define an explicit version.
    _maybe(
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

    _maybe(
        http_archive,
        name = "bazel_skylib",
        # 1.0.3, latest as of 2020-12-01
        urls = [
            "https://mirror.bazel.build/github.com/bazelbuild/bazel-skylib/releases/download/1.0.3/bazel-skylib-1.0.3.tar.gz",
            "https://github.com/bazelbuild/bazel-skylib/releases/download/1.0.3/bazel-skylib-1.0.3.tar.gz",
        ],
        sha256 = "1c531376ac7e5a180e0237938a2536de0c54d93f5c278634818e0efc952dd56c",
    )

    _maybe(
        http_archive,
        name = "io_bazel_rules_go",
        sha256 = "7904dbecbaffd068651916dce77ff3437679f9d20e1a7956bff43826e7645fcc",
        urls = [
            "https://mirror.bazel.build/github.com/bazelbuild/rules_go/releases/download/v0.25.1/rules_go-v0.25.1.tar.gz",
            "https://github.com/bazelbuild/rules_go/releases/download/v0.25.1/rules_go-v0.25.1.tar.gz",
        ],
    )

    # todo(#93) put this back in
    #http_archive(
    #    name = "bazel_gazelle",
    #    sha256 = "62ca106be173579c0a167deb23358fdfe71ffa1e4cfdddf5582af26520f1c66f",
    #    urls = [
    #        "https://mirror.bazel.build/github.com/bazelbuild/bazel-gazelle/releases/download/v0.23.0/bazel-gazelle-v0.23.0.tar.gz",
    #        "https://github.com/bazelbuild/bazel-gazelle/releases/download/v0.23.0/bazel-gazelle-v0.23.0.tar.gz",
    #    ],
    #)
    _maybe(
        git_repository,
        name = "bazel_gazelle",
        #        path = "../bazel-gazelle",
        #        branch = "other",
        commit = "f3d57a478ca0043905f818766b60bb21674eaaad",
        shallow_since = "1633489567 -0400",
        remote = "https://github.com/samhowes/bazel-gazelle",
    )

def _maybe(repo_rule, name, **kwargs):
    if name not in native.existing_rules():
        repo_rule(name = name, **kwargs)
