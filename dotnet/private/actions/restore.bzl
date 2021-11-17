load(":common.bzl", "get_nuget_files")
load("//dotnet/private:context.bzl", "make_builder_cmd")
load("//dotnet/private/actions:common.bzl", "cache_set", "declare_caches", "write_cache_manifest")
load("//dotnet/private:providers.bzl", "DotnetRestoreInfo", "MSBuildDirectoryInfo", "NuGetPackageInfo")

def restore(ctx, dotnet):
    # we don't really need this since we're declaring the directory, but this way, if the restore
    # fails, bazel will fail the build because this file wasn't created
    outputs, assets_json = _declare_restore_outputs(ctx)
    cache = declare_caches(ctx, "restore")

    files, caches = _process_deps(dotnet, ctx)
    cache_manifest = write_cache_manifest(ctx, cache, cache_set(transitive = caches))
    directory_info = ctx.attr.msbuild_directory[MSBuildDirectoryInfo]

    inputs = depset(
        direct = [ctx.file.project_file, cache_manifest],
        transitive = files + [directory_info.files],
    )

    outputs.extend([cache.result, cache.project])

    assembly_name = _get_assembly_name(ctx, directory_info)
    args, cmd_outputs = make_builder_cmd(ctx, dotnet, "restore", directory_info, assembly_name)

    outputs.extend(cmd_outputs)
    args.add_all([
        "--version",
        ctx.attr.version,
        "--package_version",
        ctx.attr.package_version,
    ])

    ctx.actions.run(
        mnemonic = "NuGetRestore",
        inputs = inputs,
        outputs = outputs,
        executable = dotnet.sdk.dotnet,
        arguments = [args],
        env = dotnet.env,
        tools = dotnet.builder.files,
    )

    return DotnetRestoreInfo(
        target_framework = ctx.attr.target_framework,
        assets_json = assets_json,
        outputs = outputs,
        files = depset(outputs, transitive = [inputs]),
        caches = cache_set([cache], transitive = caches),
        directory_info = directory_info,
        assembly_name = assembly_name,
    ), outputs

def _process_deps(dotnet, ctx):
    tfm = dotnet.config.tfm
    deps = ctx.attr.deps

    files = []
    caches = []
    for dep in getattr(dotnet.config, "tfm_deps", []):
        get_nuget_files(dep, tfm, files)

    for dep in getattr(dotnet.config, "implicit_deps", []):
        get_nuget_files(dep, tfm, files)

    for dep in deps:
        if DotnetRestoreInfo in dep:
            info = dep[DotnetRestoreInfo]

            files.append(info.files)
            caches.append(info.caches)
        elif NuGetPackageInfo in dep:
            get_nuget_files(dep, tfm, files)
        else:
            fail("Unkown dependency type: {}".format(dep))

    return files, caches

def _get_assembly_name(ctx, directory_info):
    if directory_info == None:
        return ""
    override = getattr(ctx.attr, "assembly_name", None)
    if override != None and override != "":
        return override
    parts = []

    prefix = getattr(directory_info, "assembly_name_prefix", "")
    if prefix != "":
        parts.append(prefix)

    name = ctx.attr.name[:(-1 * len("_restore"))]

    if getattr(directory_info, "use_bazel_package_for_assembly_name", False):
        if ctx.label.package != "":
            parts.extend(ctx.label.package.split("/"))

        if name != parts[-1]:
            parts.append(name)
    else:
        parts.append(name)
    return ".".join(parts)

def _declare_restore_outputs(ctx):
    outputs = []
    for d in ["restore/_/", "restore/"]:
        for x in [".assets.json", ".nuget.cache"]:
            outputs.append(ctx.actions.declare_file(d + "project" + x))
        for x in [".bazel.props", ".bazel.targets", ".nuget.g.props", ".nuget.g.targets", ".nuget.dgspec.json"]:
            outputs.append(ctx.actions.declare_file(d + ctx.attr.project_file.label.name + x))
    return outputs, outputs[0]
