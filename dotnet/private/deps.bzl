"""Dependencies of dotnet_rules"""

load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")

def dotnet_rules_dependencies():
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

def _maybe(repo_rule, name, **kwargs):
    if name not in native.existing_rules():
        repo_rule(name = name, **kwargs)
