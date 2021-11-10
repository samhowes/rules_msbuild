load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")
http_archive(
    name = "rules_msbuild",
    sha256 = "6d860cf589ba5b8f1f8a780425dd0b25ae5461cc717e99ef3190e9f88034b0c6",
    urls = ["https://github.com/samhowes/rules_msbuild/releases/download/0.0.16/rules_msbuild-0.0.16.tar.gz"],
)
load("@rules_msbuild//dotnet:deps.bzl", "msbuild_register_toolchains", "msbuild_rules_dependencies")

msbuild_rules_dependencies()
# See https://dotnet.microsoft.com/download/dotnet for valid versions
msbuild_register_toolchains(version = "host")
