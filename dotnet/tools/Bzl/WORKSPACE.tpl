load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")
http_archive(
    name = "rules_msbuild",
    sha256 = "6a8b02778a22c7571dfdc87bfea730cef3081a800f90fc4e2d5496f58927316e",
    urls = ["https://github.com/samhowes/rules_msbuild/releases/download/0.0.14/rules_msbuild-0.0.14.tar.gz"],
)
load("@rules_msbuild//dotnet:deps.bzl", "msbuild_register_toolchains", "msbuild_rules_dependencies")

msbuild_rules_dependencies()
# See https://dotnet.microsoft.com/download/dotnet for valid versions
msbuild_register_toolchains(version = "host")
