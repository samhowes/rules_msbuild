load("//dotnet/private:providers.bzl", "MSBuildDirectoryInfo")

def _directory_impl(ctx):
    root_package = getattr(ctx.attr, "assembly_name_root_package", None)
    if root_package:
        if root_package[:2] != "//":
            fail("assembly_name_root_package must be a valid bazel package")
    files = depset(ctx.files.srcs, transitive = [d[MSBuildDirectoryInfo].files for d in ctx.attr.deps])
    return [MSBuildDirectoryInfo(
        srcs = ctx.attr.srcs,
        files = files,
        assembly_name_prefix = ctx.attr.assembly_name_prefix,
        assembly_name_root_package = ctx.attr.assembly_name_root_package,
    )]

msbuild_directory = rule(
    _directory_impl,
    attrs = {
        "srcs": attr.label_list(allow_files = True),
        "assembly_name_prefix": attr.string(default = "", doc = """A string to prefix to the AssemblyName property."""),
        "assembly_name_root_package": attr.string(
            doc = """The root bazel package use when determining the AssemblyName property.

If specified, the package provided will be used to compute the AssemblyName
Given the project file bar.bam.csproj located at //foo/bar:what.csproj
* If empty: what.dll will be produced
* If //foo:__pkg__: bar.what.dll will be produced
""",
        ),
        "deps": attr.label_list(providers = [MSBuildDirectoryInfo]),
    },
)
