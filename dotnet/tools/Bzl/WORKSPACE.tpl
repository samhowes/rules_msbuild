load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")
http_archive(
    name = "rules_msbuild",
    sha256 = "ca1c320c6c71954697c0e682c4f8eb2bdd9ea55dccdae8915acd121cd05265dd",
    urls = ["https://github.com/samhowes/rules_msbuild/releases/download/0.0.15/rules_msbuild-0.0.15.tar.gz"],
)
load("@rules_msbuild//dotnet:deps.bzl", "msbuild_register_toolchains", "msbuild_rules_dependencies")

msbuild_rules_dependencies()
# See https://dotnet.microsoft.com/download/dotnet for valid versions
msbuild_register_toolchains(version = "host")
