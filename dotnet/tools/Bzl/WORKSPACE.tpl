load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")
http_archive(
    name = "rules_msbuild",
    sha256 = "106e627fe079cb664aa0d25c8514d8e3c13d6f6bad40ad8d79d418e7563fd5f8",
    urls = ["https://github.com/samhowes/rules_msbuild/releases/download/0.0.8/rules_msbuild-0.0.8.tar.gz"],
)
load("@rules_msbuild//dotnet:deps.bzl", "msbuild_register_toolchains", "msbuild_rules_dependencies")

msbuild_rules_dependencies()
# See https://dotnet.microsoft.com/download/dotnet for valid versions
msbuild_register_toolchains(version = "host")
