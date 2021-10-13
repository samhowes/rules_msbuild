load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")
http_archive(
    name = "rules_msbuild",
    sha256 = "9395124ca2c709a395cfe918aada2d7eeed93635f482a5fcbe17d6ed01bbe21a",
    urls = ["https://github.com/samhowes/rules_msbuild/releases/download/0.0.8/rules_msbuild-0.0.8.tar.gz"],
)
load("@rules_msbuild//dotnet:repositories.bzl", "dotnet_register_toolchains", "dotnet_rules_repositories")

dotnet_rules_repositories()
# See https://dotnet.microsoft.com/download/dotnet for valid versions
dotnet_register_toolchains(version = "host")
load("@rules_msbuild//dotnet:deps.bzl", "dotnet_rules_dependencies")

dotnet_rules_dependencies()
