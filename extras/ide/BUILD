load("@rules_msbuild//dotnet:defs.bzl", "msbuild_directory")

# gazelle:exclude **/*.proj
filegroup(
    name = "ide",
    srcs = [
        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Solution.props",
        "Directory.Solution.targets",
        "ide/Bazel.props",
        "ide/Bazel.targets",
        "ide/NuGet.Config",
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
        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Solution.props",
        "Directory.Solution.targets",
        "ide/Bazel.props",
        "ide/Bazel.targets",
        "ide/NuGet.Config",
    ],
)
