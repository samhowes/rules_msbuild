"""Rules for importing nuget packages"""

load("@bazel_skylib//lib:paths.bzl", "paths")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo", "NuGetFilegroupInfo", "NuGetPackageInfo", "TfmMappingInfo")

def _nuget_import_impl(ctx):
    tfms = {}
    all_files = []
    for target in ctx.attr.frameworks:
        info = target[NuGetFilegroupInfo]
        tfms[info.tfm] = info
        all_files.append(info.all_dep_files)

    return [NuGetPackageInfo(
        name = ctx.attr.name,
        version = ctx.attr.version,
        frameworks = struct(**tfms),
        all_files = depset(
            direct = ctx.files.all_files,
            transitive = all_files,
        ),
    )]

def _nuget_filegroup_impl(ctx):
    # the name is the tfm of the package this belongs to by convention
    tfm = ctx.attr.name
    dep_files = []
    runtime = []
    build_files = []
    resource_files = []
    for target in ctx.attr.deps:
        pkg = target[NuGetPackageInfo]
        dep_files.append(pkg.all_files)

        group = getattr(pkg.frameworks, tfm, None)
        if group == None:
            fail("Package {} has not been restored for target framework {}.".format(target, tfm))
        build_files.append(group.build)
        runtime.append(group.runtime)
        resource_files.append(group.resource)

    override_version = ctx.attr.override_version
    override_version = None if override_version == None or len(override_version) == 0 else override_version

    return [NuGetFilegroupInfo(
        tfm = tfm,
        override_version = override_version,
        build = depset(
            direct = ctx.files.compile + ctx.files.build,
            transitive = build_files,
        ),
        runtime = depset(
            # no files are copied when the sdk overrides the package
            direct = ctx.files.runtime if override_version == None else [],
            transitive = runtime,
        ),
        resource = depset(
            direct = ctx.attr.resource.items(),
            transitive = resource_files,
        ),
        all_dep_files = depset(
            direct = [],
            transitive = dep_files,
        ),
    )]

def _tfm_mapping_impl(ctx):
    return [TfmMappingInfo(dict = ctx.attr.tfm_mapping)]

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
        "override_version": attr.string(
            doc = "Pacakges that are overridden have assemblies that are " +
                  "packaged with the target framework and will not get copied to the output directory.",
        ),
        "deps": attr.label_list(
            mandatory = True,
            providers = [NuGetPackageInfo],
        ),
        "build": attr.label_list(doc = "Assemblies that are imported into the project.", allow_files = True),
        "compile": attr.label_list(doc = "Assemblies that are inputs to the compiler.", allow_files = True),
        "runtime": attr.label_list(doc = "Files that are copied to the output directory.", allow_files = True),
        "resource": attr.label_keyed_string_dict(doc = "Resource files in the form label: locale", allow_files = True),
    },
)

tfm_mapping = rule(
    _tfm_mapping_impl,
    attrs = {
        "tfm_mapping": attr.string_dict(mandatory = True),
    },
)
