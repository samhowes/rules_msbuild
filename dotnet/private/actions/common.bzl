load("//dotnet/private:providers.bzl", "NuGetPackageInfo")

def write_cache_manifest(ctx, caches):
    cache_manifest = ctx.actions.declare_file(ctx.attr.name + ".input_caches")
    ctx.actions.write(cache_manifest, "\n".join([c.path for c in caches.to_list()]))
    return cache_manifest

def get_nuget_files(dep, tfm, files):
    pkg = dep[NuGetPackageInfo]
    framework_info = pkg.frameworks.get(tfm, None)
    if framework_info == None:
        fail("TargetFramework {} was not fetched for pkg dep {}. Fetched tfms: {}.".format(
            tfm,
            pkg.name,
            ", ".join([k for k, v in pkg.frameworks.items()]),
        ))
    files.append(framework_info.all_dep_files)

def add_binlog(ctx, outputs):
    if True:
        # todo(#51) disable when not debugging the build
        binlog = ctx.actions.declare_file(ctx.attr.name + ".binlog")
        outputs.append(binlog)
        return binlog
    return None
