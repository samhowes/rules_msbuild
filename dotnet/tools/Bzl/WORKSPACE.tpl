load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")
http_archive(
    name = "rules_msbuild",
    sha256 = "2427b007806fcad3b37d766b26692d27a5173805a3d3f2fc8c607c4aa6a35b8c",
    urls = ["https://github.com/samhowes/rules_msbuild/releases/download/0.0.10/rules_msbuild-0.0.10.tar.gz"],
)
load("@rules_msbuild//dotnet:deps.bzl", "msbuild_register_toolchains", "msbuild_rules_dependencies")

msbuild_rules_dependencies()
# See https://dotnet.microsoft.com/download/dotnet for valid versions
msbuild_register_toolchains(version = "host")
