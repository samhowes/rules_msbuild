load("//dotnet/private:providers.bzl", "DotnetLibraryInfo")
load("//dotnet/private/actions:pack.bzl", "pack")
load(":msbuild.bzl", "TOOLCHAINS")

def _nuget_package_impl(ctx):
    pkg = pack(ctx)
    return [
        DefaultInfo(files = depset([pkg])),
    ]

nuget_package = rule(
    _nuget_package_impl,
    attrs = {
        "version": attr.string(mandatory = True),
        "target": attr.label(
            mandatory = True,
            providers = [DotnetLibraryInfo],
        ),
    },
    toolchains = TOOLCHAINS,
)
