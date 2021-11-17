load("//dotnet:defs.bzl", "msbuild_directory")

msbuild_directory(
    name = "msbuild_defaults",
    srcs = [
        "Directory.Build.props",
        "Directory.Build.targets",
    ],
    assembly_name_prefix = "Foo",
    assembly_name_root_package = "//tests/examples",
    visibility = [":__subpackages__"],
)
