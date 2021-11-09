load("@rules_msbuild//dotnet:defs.bzl", "msbuild_directory")

# gazelle:exclude **/*.proj
filegroup(
    name = "ide",
    srcs = [
        "Bazel.props",
        "Bazel.targets",
        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Solution.props",
        "Directory.Solution.targets",
    ],
    visibility = ["//visibility:public"],
)

sh_test(
    name = "ide_test",
    srcs = ["test.sh"],
    args = ["$(location testprops.proj)"],
    data = [
        "testprops.proj",
        ":ide",
    ],
    env_inherit = [
        "HOME",
        "DOTNET_CLI_HOME",
    ],
    tags = ["manual"],
)

msbuild_directory(
    name = "msbuild_defaults",
    srcs = [
        "Bazel.props",
        "Bazel.targets",
        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Solution.props",
        "Directory.Solution.targets",
    ],
    visibility = ["//:__subpackages__"],
)
