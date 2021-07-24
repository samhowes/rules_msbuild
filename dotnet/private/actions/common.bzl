load("//dotnet/private:providers.bzl", "DotnetCacheInfo", "NuGetPackageInfo")

def declare_caches(ctx, action_name, project_cache):
    project = None
    if action_name == "build" or action_name == "restore":
        # only these actions evaluate the project file, others use a cached value
        project = ctx.actions.declare_file(ctx.file.project_file.basename + ".%s.cache" % action_name)
    else:
        if project_cache == None:
            fail("internal error: project_cache must be specified for %s" % action_name)
        project = project_cache
    return DotnetCacheInfo(
        project_path = ctx.file.project_file.short_path,
        result = ctx.actions.declare_file(ctx.attr.name + ".cache"),
        project = project,
    )

def cache_set(direct = [], transitive = []):
    return depset(direct, order = "postorder", transitive = transitive)

def write_cache_manifest(ctx, output, caches):
    projects = {}
    results = []
    for c in caches.to_list():
        if c.project == None:
            print("wtf")
        projects[c.project_path] = c.project.path
        results.append(c.result.path)

    manifest = dict(
        output = dict(
            project = output.project.path if output.project != None else None,
            results = output.result.path,
        ),
        projects = projects,
        results = results,
    )
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
