load("//dotnet:defs.bzl", "msbuild_directory")

msbuild_directory(
    name = "msbuild_defaults",
    srcs = [
        "Directory.Build.props",
        "Directory.Build.targets",
    ],
    visibility = [":__subpackages__"],
)
