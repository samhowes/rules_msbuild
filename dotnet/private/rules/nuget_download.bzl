"""Rules for importing nuget packages"""

load("@bazel_skylib//lib:paths.bzl", "paths")
load(
    "//dotnet/private:providers.bzl",
    "DotnetLibraryInfo",
    "FrameworkInfo",
    "NuGetPackageFrameworkVersionInfo",
    "NuGetPackageInfo",
    "NuGetPackageVersionInfo",
    "TfmMappingInfo",
)

def _nuget_package_download_impl(ctx):
    tfms = {}
    for target in ctx.attr.framework_versions:
        info = target[NuGetPackageFrameworkVersionInfo]
        tfms.setdefault(info.tfm, []).append(info)

    for tfm, info_list in tfms.items():
        tfms[tfm] = depset([], transitive = [i.all_files for i in info_list])

    return [NuGetPackageInfo(
        name = ctx.attr.name,
        frameworks = tfms,
    )]

def _nuget_package_framework_version_impl(ctx):
    # the name is the tfm of the package this belongs to by convention
    tfm = ctx.attr.name.split("-")[-1]
    version = ctx.attr.version[NuGetPackageVersionInfo]
    files = [version.all_files]
    for target in ctx.attr.deps:
        version_info = target[NuGetPackageFrameworkVersionInfo]
        files.append(version_info.all_files)

    return [NuGetPackageFrameworkVersionInfo(
        tfm = tfm,
        version = version.version,
        all_files = depset(
            direct = [],
            transitive = files,
        ),
    )]

def _nuget_package_version_impl(ctx):
    return [NuGetPackageVersionInfo(
        version = ctx.attr.name,
        all_files = depset(ctx.files.all_files),
    )]

def _tfm_mapping_impl(ctx):
    return [TfmMappingInfo(dict = dict(
        [
            (f[FrameworkInfo].tfm, f[FrameworkInfo])
            for f in ctx.attr.frameworks
        ],
    ))]

def _framework_info_impl(ctx):
    return [FrameworkInfo(tfm = ctx.attr.name, implicit_deps = ctx.attr.implicit_deps)]

nuget_package_download = rule(
    _nuget_package_download_impl,
    attrs = {
        "framework_versions": attr.label_list(mandatory = True, providers = [NuGetPackageFrameworkVersionInfo]),
    },
    executable = False,
)

nuget_package_framework_version = rule(
    _nuget_package_framework_version_impl,
    attrs = {
        "version": attr.label(
            mandatory = True,
            providers = [NuGetPackageVersionInfo],
        ),
        "deps": attr.label_list(
            mandatory = True,
            providers = [NuGetPackageFrameworkVersionInfo],
        ),
    },
)

nuget_package_version = rule(
    _nuget_package_version_impl,
    attrs = {
        "all_files": attr.label_list(mandatory = True, allow_files = True),
    },
)

tfm_mapping = rule(
    _tfm_mapping_impl,
    attrs = {
        "frameworks": attr.label_list(
            mandatory = True,
            providers = [FrameworkInfo],
        ),
    },
)

framework_info = rule(
    _framework_info_impl,
    attrs = {
        "implicit_deps": attr.label_list(providers = [NuGetPackageInfo]),
    },
)
