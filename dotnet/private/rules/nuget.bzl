load("//dotnet/private:providers.bzl", "DotnetLibraryInfo", "DotnetPublishInfo")
load("//dotnet/private/actions:pack.bzl", "pack")
load(":msbuild.bzl", "TOOLCHAINS")

def _nuget_package_impl(ctx):
    pkg = pack(ctx)
    return [
        DefaultInfo(files = depset([pkg]), runfiles = ctx.runfiles([pkg])),
    ]

nuget_package = rule(
    _nuget_package_impl,
    attrs = {
        "project_file": attr.label(mandatory = True, allow_single_file = True),
        "version": attr.string(mandatory = True),
        "target": attr.label(
            mandatory = True,
            providers = [DotnetPublishInfo],
        ),
    },
    toolchains = TOOLCHAINS,
)
