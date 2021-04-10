"""Rules for importing nuget packages"""

load("@bazel_skylib//lib:paths.bzl", "paths")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo", "NuGetFilegroupInfo", "NuGetPackageInfo")

def _nuget_import_impl(ctx):
    tfms = {}
    all_files = []
    for target in ctx.attr.frameworks:
        info = target[NuGetFilegroupInfo]
        tfms[info.name] = info

    return [DotnetLibraryInfo(
        assembly = None,
        pdb = None,
        deps = depset(),
        package_info = NuGetPackageInfo(
            name = ctx.attr.name,
            version = ctx.attr.version,
            frameworks = struct(**tfms),
            all_files = depset(ctx.files.all_files),
        ),
    )]

def _nuget_filegroup_impl(ctx):
    return [NuGetFilegroupInfo(
        name = ctx.attr.name,
        compile = depset(ctx.files.compile),
        runtime = depset(ctx.files.runtime),
    )]

nuget_import = rule(
    _nuget_import_impl,
    attrs = {
        "version": attr.string(mandatory = True),
        "frameworks": attr.label_list(mandatory = True, providers = [NuGetFilegroupInfo]),
        "all_files": attr.label_list(mandatory = True, allow_files = True),
    },
    executable = False,
    toolchains = ["@my_rules_dotnet//dotnet:toolchain"],
)

nuget_filegroup = rule(
    _nuget_filegroup_impl,
    attrs = {
        "compile": attr.label_list(doc = "Assemblies that are inputs to the compiler.", allow_files = True),
        "runtime": attr.label_list(doc = "Files that are copied to the output directory.", allow_files = True),
    },
)
