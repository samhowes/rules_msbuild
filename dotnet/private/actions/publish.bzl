load("@bazel_skylib//lib:paths.bzl", "paths")
load(":common.bzl", "declare_caches", "write_cache_manifest")
load("//dotnet/private:context.bzl", "dotnet_exec_context", "make_builder_cmd")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo")

def publish(ctx):
    info = ctx.attr.target[DotnetLibraryInfo]
    restore = info.restore

    dotnet = dotnet_exec_context(ctx, False, False, restore.target_framework)

    output_dir = ctx.actions.declare_directory(paths.join("publish", dotnet.config.tfm))

    cache = declare_caches(ctx, "publish")
    cache_manifest = write_cache_manifest(ctx, cache, info.caches)

    args, cmd_outputs = make_builder_cmd(ctx, dotnet, "publish")

    #    print("\n".join([f.short_path for f in info.files.to_list()]))

    inputs = depset([cache_manifest], transitive = [info.files])
    outputs = [output_dir, cache.project, cache.result] + cmd_outputs

    ctx.actions.run(
        mnemonic = "DotnetPublish",
        inputs = inputs,
        outputs = outputs,
        executable = dotnet.sdk.dotnet,
        arguments = [args],
        env = dotnet.env,
        tools = dotnet.builder.files,
    )
    return output_dir
