load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")
http_archive(
    name = "rules_msbuild",
    sha256 = "5bb9d506ae025796a9d4e5dada6408d6cb255c1dc52e1f11e6eb93ffc838f341",
    urls = ["https://github.com/samhowes/rules_msbuild/releases/download/0.0.17/rules_msbuild-0.0.17.tar.gz"],
)
load("@rules_msbuild//dotnet:deps.bzl", "msbuild_register_toolchains", "msbuild_rules_dependencies")

msbuild_rules_dependencies()
# See https://dotnet.microsoft.com/download/dotnet for valid versions
msbuild_register_toolchains(version = "host")
