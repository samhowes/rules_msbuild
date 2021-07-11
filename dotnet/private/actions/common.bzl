load("//dotnet/private:providers.bzl", "NuGetPackageInfo")

def declare_cache(ctx):
    return ctx.actions.declare_file(ctx.attr.name + ".cache")

def write_cache_manifest(ctx, projects):
    manifest = dict(
        projects = projects,
    )
    print(json.encode_indent(manifest))
    file = ctx.actions.declare_file(ctx.attr.name + ".cache_manifest")
    ctx.actions.write(file, json.encode(manifest))
    return file

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

def add_diagnostics(ctx, dotnet, outputs):
    if dotnet.config.diag:
        binlog = ctx.actions.declare_file(ctx.attr.name + ".binlog")
        outputs.append(binlog)
        if dotnet.builder != None:
            dot = ctx.actions.declare_file(ctx.attr.name + ".dot")
            outputs.append(dot)
        return binlog
    return None
