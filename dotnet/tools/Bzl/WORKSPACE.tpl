load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")
http_archive(
    name = "rules_msbuild",
    sha256 = "7be9e301e2135c12c62f453f6f4583e21500712b2b48996121cd52bcfb9e06bf",
    urls = ["https://github.com/samhowes/rules_msbuild/releases/download/0.0.9/rules_msbuild-0.0.9.tar.gz"],
)
load("@rules_msbuild//dotnet:deps.bzl", "msbuild_register_toolchains", "msbuild_rules_dependencies")

msbuild_rules_dependencies()
# See https://dotnet.microsoft.com/download/dotnet for valid versions
msbuild_register_toolchains(version = "host")
