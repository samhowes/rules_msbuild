load("@bazel_skylib//lib:paths.bzl", "paths")
load(":common.bzl", "declare_caches", "write_cache_manifest")
load("//dotnet/private:context.bzl", "dotnet_exec_context", "make_builder_cmd")
load("//dotnet/private:providers.bzl", "DotnetPublishInfo")

def pack(ctx):
    info = ctx.attr.target[DotnetPublishInfo]
    restore = info.restore

    dotnet = dotnet_exec_context(ctx, False, False, restore.target_framework)

    package_id = getattr(ctx.attr, "package_id", None)
    if not package_id:
        package_id = paths.split_extension(ctx.file.project_file.basename)[0]
    nupkg = ctx.actions.declare_file(package_id + "." + ctx.attr.version + ".nupkg")

    cache = declare_caches(ctx, "pack")
    cache_manifest = write_cache_manifest(ctx, cache, info.caches)

    args, cmd_outputs = make_builder_cmd(ctx, dotnet, "pack", restore.directory_info)
    args.add_all(["--version", ctx.attr.version, "--runfiles_manifest", info.runfiles_manifest])

    inputs = depset(
        [cache_manifest, info.runfiles_manifest],
        transitive = [info.files, info.library.runfiles],
    )
    outputs = [nupkg, cache.result] + cmd_outputs

    ctx.actions.run(
        mnemonic = "DotnetPack",
        inputs = inputs,
        outputs = outputs,
        executable = dotnet.sdk.dotnet,
        arguments = [args],
        env = dotnet.env,
        tools = dotnet.builder.files,
    )
    return nupkg
