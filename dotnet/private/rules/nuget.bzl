"""Rules for importing nuget packages"""

load("@bazel_skylib//lib:paths.bzl", "paths")
load(
    "//dotnet/private:providers.bzl",
    "DotnetLibraryInfo",
    "FrameworkInfo",
    "NuGetFilegroupInfo",
    "NuGetPackageInfo",
    "NuGetPackageVersionInfo",
    "TfmMappingInfo",
)

def _nuget_import_impl(ctx):
    tfms = {}
    for target in ctx.attr.frameworks:
        info = target[NuGetFilegroupInfo]
        tfms[info.tfm] = info

    return [NuGetPackageInfo(
        name = ctx.attr.name,
        frameworks = tfms,
    )]

def _nuget_package_version_impl(ctx):
    return [NuGetPackageVersionInfo(
        version = ctx.attr.name,
        all_files = depset(ctx.files.all_files),
    )]

def _nuget_filegroup_impl(ctx):
    # the name is the tfm of the package this belongs to by convention
    tfm = ctx.attr.name
    version = ctx.attr.version[NuGetPackageVersionInfo]
    files = [version.all_files]
    for target in ctx.attr.deps:
        pkg = target[NuGetPackageInfo]
        group = pkg.frameworks.get(tfm, None)
        if group == None:
            fail("Package {} has not been restored for target framework {}.".format(target, tfm))

        files.append(group.all_dep_files)

    return [NuGetFilegroupInfo(
        tfm = tfm,
        version = version.version,
        all_dep_files = depset(
            direct = [],
            transitive = files,
        ),
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

nuget_import = rule(
    _nuget_import_impl,
    attrs = {
        "frameworks": attr.label_list(mandatory = True, providers = [NuGetFilegroupInfo]),
    },
    executable = False,
)

nuget_package_version = rule(
    _nuget_package_version_impl,
    attrs = {
        "all_files": attr.label_list(mandatory = True, allow_files = True),
    },
)

nuget_filegroup = rule(
    _nuget_filegroup_impl,
    attrs = {
        "version": attr.label(mandatory = True, providers = [NuGetPackageVersionInfo]),
        "deps": attr.label_list(
            mandatory = True,
            providers = [NuGetPackageInfo],
        ),
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
