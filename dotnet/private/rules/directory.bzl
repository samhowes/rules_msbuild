load("//dotnet/private:providers.bzl", "MSBuildDirectoryInfo")

def _directory_impl(ctx):
    files = depset(ctx.files.srcs, transitive = [d[MSBuildDirectoryInfo].files for d in ctx.attr.deps])
    return [MSBuildDirectoryInfo(
        srcs = ctx.attr.srcs,
        files = files,
        assembly_name_prefix = ctx.attr.assembly_name_prefix,
        use_bazel_package_for_assembly_name = ctx.attr.use_bazel_package_for_assembly_name,
    )]

msbuild_directory = rule(
    _directory_impl,
    attrs = {
        "srcs": attr.label_list(allow_files = True),
        "assembly_name_prefix": attr.string(default = "", doc = """A string to prefix to the AssemblyName property."""),
        "use_bazel_package_for_assembly_name": attr.bool(
            doc = """Whether to use the bazel package to determine the AssemblyName property.

Given the project file bar.bam.csproj located at //foo/bar:what.csproj
* If false: what.dll will be produced
* If true: foo.bar.what.dll will be produced
""",
        ),
        "deps": attr.label_list(providers = [MSBuildDirectoryInfo]),
    },
)
