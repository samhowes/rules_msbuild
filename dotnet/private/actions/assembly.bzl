load("//dotnet/private:providers.bzl", "DotnetLibraryInfo", "DotnetRestoreInfo", "MSBuildDirectoryInfo")
load("//dotnet/private:context.bzl", "make_builder_cmd")
load(":common.bzl", "cache_set", "declare_caches", "get_nuget_files", "write_cache_manifest")
load("@bazel_skylib//lib:paths.bzl", "paths")

def build_assembly(ctx, dotnet):
    restore = ctx.attr.restore[DotnetRestoreInfo]

    output_dir = ctx.actions.declare_directory(dotnet.config.output_dir_name)
    assembly = ctx.actions.declare_file(paths.join(output_dir.basename, ctx.attr.name + ".dll"))

    intermediate_dir = ctx.actions.declare_directory(paths.join("obj", dotnet.config.tfm))

    # we don't need this file, but adding will make sure bazel fails the build if it isn't created because msbuild
    # didn't listen to our paths
    intermediate_assembly = ctx.actions.declare_file(paths.join("obj", dotnet.config.tfm, assembly.basename))

    cache = declare_caches(ctx, "build")
    files, caches, runfiles = _process_deps(ctx, dotnet)
    caches = cache_set(transitive = caches)
    cache_manifest = write_cache_manifest(ctx, cache, caches)
    directory_info = ctx.attr.msbuild_directory[MSBuildDirectoryInfo]
    args, cmd_outputs = make_builder_cmd(ctx, dotnet, "build", directory_info)

    protos = _getProtos(ctx)

    inputs = depset(
        [cache_manifest, ctx.file.project_file] + ctx.files.srcs + ctx.files.content,
        transitive = files + [restore.files] + protos,
    )

    outputs = [output_dir, assembly, intermediate_dir, intermediate_assembly, cache.project, cache.result] + cmd_outputs

    ctx.actions.run(
        mnemonic = "MSBuild",
        inputs = inputs,
        outputs = outputs,
        executable = dotnet.sdk.dotnet,
        arguments = [args],
        env = dotnet.env,
        tools = dotnet.builder.files,
    )

    info = DotnetLibraryInfo(
        assembly = assembly,
        output_dir = output_dir,
        files = depset(direct = outputs, transitive = [inputs]),
        caches = cache_set([cache], transitive = [caches]),
        runfiles = depset(ctx.files.data, transitive = runfiles),
        project_cache = cache.project,
        restore = restore,
    )

    return info, outputs

def _getProtos(ctx):
    deps = []
    for p in ctx.attr.protos:
        info = p[ProtoInfo]
        deps.append(depset(info.direct_sources, transitive = [info.transitive_sources]))
    return deps

def _process_deps(ctx, dotnet):
    files = []
    caches = []
    runfiles = []

    for d in dotnet.config.implicit_deps:
        get_nuget_files(d, dotnet.config.tfm, files)

    for d in ctx.attr.deps:
        if DotnetLibraryInfo in d:
            info = d[DotnetLibraryInfo]
            files.append(info.files)
            runfiles.append(info.runfiles)
            caches.append(info.caches)

    return files, caches, runfiles
