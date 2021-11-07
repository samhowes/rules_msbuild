load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")
http_archive(
    name = "rules_msbuild",
    sha256 = "921a0540df0a4f2e97369c209aaa17c4ff472b6410c745016218cb7db5e3ccb5",
    urls = ["https://github.com/samhowes/rules_msbuild/releases/download/0.0.12/rules_msbuild-0.0.12.tar.gz"],
)
load("@rules_msbuild//dotnet:deps.bzl", "msbuild_register_toolchains", "msbuild_rules_dependencies")

msbuild_rules_dependencies()
# See https://dotnet.microsoft.com/download/dotnet for valid versions
msbuild_register_toolchains(version = "host")
