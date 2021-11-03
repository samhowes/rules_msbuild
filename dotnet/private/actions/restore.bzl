load(":common.bzl", "get_nuget_files")
load("//dotnet/private:context.bzl", "make_builder_cmd")
load("//dotnet/private/actions:common.bzl", "cache_set", "declare_caches", "write_cache_manifest")
load("//dotnet/private:providers.bzl", "DotnetRestoreInfo", "MSBuildDirectoryInfo", "NuGetPackageInfo")

def restore(ctx, dotnet):
    # we don't really need this since we're declaring the directory, but this way, if the restore
    # fails, bazel will fail the build because this file wasn't created
    assets_json = ctx.actions.declare_file("restore/project.assets.json")
    restore_dir = ctx.actions.declare_directory("restore")

    cache = declare_caches(ctx, "restore")

    files, caches = _process_deps(dotnet, ctx)
    cache_manifest = write_cache_manifest(ctx, cache, cache_set(transitive = caches))
    directory_info = ctx.attr.msbuild_directory[MSBuildDirectoryInfo]

    inputs = depset(
        direct = [ctx.file.project_file, cache_manifest],
        transitive = files + [directory_info.files],
    )

    outputs = [assets_json, restore_dir, cache.result, cache.project]

    args, cmd_outputs = make_builder_cmd(ctx, dotnet, "restore", directory_info)
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
        output_dir = restore_dir,
        files = depset(outputs, transitive = [inputs]),
        caches = cache_set([cache], transitive = caches),
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
