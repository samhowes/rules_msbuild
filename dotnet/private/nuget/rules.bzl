"""Rules for importing nuget packages"""

load("//dotnet/private:providers.bzl", "DotnetLibraryInfo")

def _nuget_import_impl(ctx):
    return [DotnetLibraryInfo(
        assembly = None,
        pdb = None,
        deps = depset(),
    )]

nuget_import = rule(
    _nuget_import_impl,
    attrs = {
        "package_name": attr.string(mandatory = True),
        "version": attr.string(mandatory = True),
    },
    executable = False,
    toolchains = ["@my_rules_dotnet//dotnet:toolchain"],
)
