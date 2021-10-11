load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")
http_archive(
    name = "rules_msbuild",
    sha256 = "67818f4d2f193766cef952ba8efe72f26ec6323606c681baf58e437bc9ca94ac",
    urls = ["https://github.com/samhowes/rules_msbuild/releases/download/0.0.6/rules_msbuild-0.0.6.tar.gz"],
)
load("@rules_msbuild//dotnet:repositories.bzl", "dotnet_register_toolchains", "dotnet_rules_repositories")

dotnet_rules_repositories()
# See https://dotnet.microsoft.com/download/dotnet for valid versions
dotnet_register_toolchains(version = "host")

# gazelle and launcher dependencies
load("@io_bazel_rules_go//go:deps.bzl", "go_register_toolchains", "go_rules_dependencies")
load("@bazel_gazelle//:deps.bzl", "gazelle_dependencies")
load("@rules_msbuild//dotnet:deps.bzl", "dotnet_rules_dependencies")

dotnet_rules_dependencies()
go_rules_dependencies()
go_register_toolchains(version = "1.16.2")
gazelle_dependencies()
