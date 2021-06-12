workspace(name = "@@workspace_name@@")

load("@bazel_tools//tools/build_defs/repo:git.bzl", "http_archive")
git_repository(
    name = "rules_msbuild",
    tag = "stable",
    remote = "https://github.com/samhowes/rules_msbuild"
)
load("@rules_msbuild//dotnet:deps.bzl", "dotnet_register_toolchains", "dotnet_rules_dependencies")

dotnet_rules_dependencies()
# See https://dotnet.microsoft.com/download/dotnet for valid versions
dotnet_register_toolchains(version = "3.1.100")