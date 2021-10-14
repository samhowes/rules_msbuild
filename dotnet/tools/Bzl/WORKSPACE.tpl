load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")
http_archive(
    name = "rules_msbuild",
    sha256 = "96df9be286fff1fadf61f46f64065158a2a1bb8d2e61f39d4ec4affa443012a9",
    urls = ["https://github.com/samhowes/rules_msbuild/releases/download/0.0.8/rules_msbuild-0.0.8.tar.gz"],
)
load("@rules_msbuild//dotnet:repositories.bzl", "dotnet_register_toolchains", "dotnet_rules_repositories")

dotnet_rules_repositories()
# See https://dotnet.microsoft.com/download/dotnet for valid versions
dotnet_register_toolchains(version = "host")
