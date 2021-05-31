load("@bazel_skylib//lib:paths.bzl", "paths")
load(":common.bzl", "write_cache_manifest")
load("//dotnet/private:context.bzl", "dotnet_exec_context", "make_builder_cmd")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo")

def publish(ctx):
    info = ctx.attr.target[DotnetLibraryInfo]
    restore = info.restore

    dotnet = dotnet_exec_context(ctx, False, False, restore.target_framework)

    output_dir = ctx.actions.declare_directory(paths.join("publish", dotnet.config.tfm))

    args, cmd_outputs = make_builder_cmd(ctx, dotnet, "publish")

    cache_manifest = write_cache_manifest(ctx, depset([info.build_cache]))

    inputs = depset(
        [info.output_dir, info.intermediate_dir, info.build_cache, cache_manifest],
        transitive = [info.dep_files, dotnet.sdk.runfiles],
    )

    outputs = [output_dir] + cmd_outputs

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
