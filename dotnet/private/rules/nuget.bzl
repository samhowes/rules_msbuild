"""Rules for importing nuget packages"""

load("//dotnet/private:providers.bzl", "DotnetLibraryInfo", "NugetPackageInfo", "NugetPreRestoreInfo")
load("//dotnet/private/rules:core.bzl", "DEPS_ATTR", "TFM_ATTR")
load("//dotnet/private/actions:restore.bzl", "restore")
load("//dotnet/private/actions:pre_restore.bzl", "pre_restore")

def fake_nuget_import(name, version):
    nuget_import(
        name = name,
        package_name = name,
        version = version,
    )

def _nuget_packages_impl(ctx):
    return [NugetPackageInfo(
        package_versions = ctx.attr.package_versions,
    )]

# def _nuget_restore_impl(ctx):
#     files = restore(ctx)
#     return [NugetPackageInfo(
#         package_versions = ctx.attr.package_versions,
#     )]

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

def _nuget_pre_restore_impl(ctx):
    """emits actions for generating files to configure the restore of a dotnet assembly"""
    props_file, tfm = pre_restore(ctx)
    return [
        DefaultInfo(files = depset([props_file])),
        NugetPreRestoreInfo(
            primary_name = ctx.attr.primary_name,
            props_file = props_file,
            tfms = [tfm],
        ),
    ]

nuget_pre_restore = rule(
    implementation = _nuget_pre_restore_impl,
    attrs = {
        "primary_name": attr.string(mandatory = True, doc = "The restore target."),
        "target_framework": TFM_ATTR,
        "deps": DEPS_ATTR,
        "_props_template": attr.label(
            default = Label("//dotnet/private/rules:nuget_deps.tpl.props"),
            allow_single_file = True,
        ),
    },
)

# nuget_restore = rule(
#     implementation = _nuget_restore_impl,
#     attrs = {
#         "primary_name": PRIMARY_NAME_ATTR,
#         "target_framework": TFM_ATTR,
#         "pre_restore": attr.label(allow_single_file = True, doc = "The pre_restore label for this target."),
#     },
# )

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

nuget_packages = rule(
    _nuget_packages_impl,
    attrs = {
        "package_versions": attr.string_dict(mandatory = True),
    },
    executable = False,
    toolchains = ["@my_rules_dotnet//dotnet:toolchain"],
)
