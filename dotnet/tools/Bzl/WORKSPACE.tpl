workspace(name = "@@workspace_name@@")

load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")
http_archive(
    name = "rules_msbuild",
    sha256 = "57137015c9c0a164c89430adbd835e3926376dc79e416b1bfe19a481b7b7ffa6",
    urls = ["https://github.com/samhowes/rules_msbuild/releases/download/0.0.1/bazel-out/x64_windows-fastbuild/bin/rules_msbuild.tar.gz"],
)
load("@rules_msbuild//dotnet:repositories.bzl", "dotnet_register_toolchains", "dotnet_rules_repositories")

dotnet_rules_repositories()
# See https://dotnet.microsoft.com/download/dotnet for valid versions
dotnet_register_toolchains(version = "host")

load("@io_bazel_rules_go//go:deps.bzl", "go_register_toolchains", "go_rules_dependencies")
load("@bazel_gazelle//:deps.bzl", "gazelle_dependencies")
load("@rules_msbuild//dotnet:deps.bzl", "dotnet_rules_dependencies")

dotnet_rules_dependencies()
go_rules_dependencies()
go_register_toolchains(version = "1.16.2")
gazelle_dependencies()
