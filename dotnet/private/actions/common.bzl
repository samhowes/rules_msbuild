load("//dotnet/private:providers.bzl", "DotnetCacheInfo", "NuGetPackageInfo")
load("//dotnet/private/util:util.bzl", "to_manifest_path")

def declare_caches(ctx, action_name):
    project = None
    if action_name == "build" or action_name == "restore" or action_name == "publish":
        # only these actions evaluate the project file, others use a cached value
        project = ctx.actions.declare_file(ctx.file.project_file.basename + ".%s.cache" % action_name)
    return DotnetCacheInfo(
        project_path = to_manifest_path(ctx, ctx.file.project_file),
        result = ctx.actions.declare_file(ctx.attr.name + ".cache"),
        project = project,
    )

def cache_set(direct = [], transitive = []):
    return depset(direct, order = "postorder", transitive = transitive)

def write_cache_manifest(ctx, output, caches):
    projects = {}
    results = []
    for c in caches.to_list():
        if c.project != None:
            projects[c.project_path] = to_manifest_path(ctx, c.project, True)
        results.append(c.result.path)

    manifest = dict(
        output = dict(
            project = to_manifest_path(ctx, output.project, True) if output.project != None else None,
            result = to_manifest_path(ctx, output.result, True),
        ),
        projects = projects,
        results = results,
    )
    file = ctx.actions.declare_file(ctx.attr.name + ".cache_manifest")
    ctx.actions.write(file, json.encode_indent(manifest))
    return file

def get_nuget_files(dep, tfm, files):
    pkg = dep[NuGetPackageInfo]
    framework_files = pkg.frameworks.get(tfm, None)
    if framework_files == None:
        fail("TargetFramework {} was not fetched for pkg dep {}. Fetched tfms: {}.".format(
            tfm,
            pkg.name,
            ", ".join([k for k, v in pkg.frameworks.items()]),
        ))
    files.append(framework_files)

def add_diagnostics(ctx, dotnet, outputs):
    if dotnet.config.diag:
        binlog = ctx.actions.declare_file(ctx.attr.name + ".binlog")
        outputs.append(binlog)
        if dotnet.builder != None:
            dot = ctx.actions.declare_file(ctx.attr.name + ".dot")
            outputs.append(dot)
        return binlog
    return None
