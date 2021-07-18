load("@bazel_skylib//lib:paths.bzl", "paths")
load(":common.bzl", "cache_set", "declare_caches", "write_cache_manifest")
load("//dotnet/private:context.bzl", "dotnet_exec_context", "make_builder_cmd")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo", "DotnetPublishInfo")

def publish(ctx):
    info = ctx.attr.target[DotnetLibraryInfo]
    restore = info.restore

    dotnet = dotnet_exec_context(ctx, False, False, restore.target_framework)

    output_dir = ctx.actions.declare_directory(paths.join("publish", dotnet.config.tfm))

    cache = declare_caches(ctx, "publish", info.project_cache)

    caches = info.caches
    cache_manifest = write_cache_manifest(ctx, cache, caches)

    args, cmd_outputs = make_builder_cmd(ctx, dotnet, "publish")

    inputs = depset([cache_manifest], transitive = [info.files])
    outputs = [output_dir, cache.result] + cmd_outputs

    ctx.actions.run(
        mnemonic = "DotnetPublish",
        inputs = inputs,
        outputs = outputs,
        executable = dotnet.sdk.dotnet,
        arguments = [args],
        env = dotnet.env,
        tools = dotnet.builder.files,
    )

    publish_info = DotnetPublishInfo(
        output_dir = output_dir,
        files = depset(direct = outputs, transitive = [inputs]),
        caches = cache_set([cache], transitive = [caches]),
        library = info,
        restore = restore,
    )

    return publish_info
