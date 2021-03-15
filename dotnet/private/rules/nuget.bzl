"""Rules for importing nuget packages"""

load("//dotnet/private:providers.bzl", "DotnetLibraryInfo", "NugetPackageInfo", "NugetPreRestoreInfo")
load("//dotnet/private/actions:restore.bzl", "restore")

def fake_nuget_import(name, version):
    nuget_import(
        name = name,
        package_name = name,
        version = version,
    )

def _nuget_import_impl(ctx):
    return [DotnetLibraryInfo(
        assembly = None,
        pdb = None,
        deps = depset(),
        package_info = NugetPackageInfo(
            name = ctx.attr.name,
            version = ctx.attr.version,
        ),
    )]

def _nuget_restore_impl(ctx):
    """emits actions for restoring packages for a dotnet assembly"""
    files = restore(ctx)
    return [
        DefaultInfo(files = depset(files)),
    ]

nuget_restore = rule(
    _nuget_restore_impl,
    attrs = {
        "target_frameworks": attr.string_list(mandatory = True, allow_empty = False, doc = "The target frameworks to restore."),
        "deps": attr.label_list(
            providers = [NugetPreRestoreInfo],
        ),
        "_restore_template": attr.label(
            allow_single_file = True,
            default = Label("//dotnet/private/rules:nuget_restore.tpl.proj"),
        ),
    },
    executable = False,
    toolchains = ["@my_rules_dotnet//dotnet:toolchain"],
)

nuget_import = rule(
    _nuget_import_impl,
    attrs = {
        "package_name": attr.string(mandatory = True),
        "version": attr.string(mandatory = True),
    },
    executable = False,
    toolchains = ["@my_rules_dotnet//dotnet:toolchain"],
)
