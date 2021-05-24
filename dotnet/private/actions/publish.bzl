load("@bazel_skylib//lib:paths.bzl", "paths")
load("//dotnet/private:context.bzl", "dotnet_exec_context", "make_builder_cmd")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo")

def publish(ctx):
    info = ctx.attr.target[DotnetLibraryInfo]
    restore = info.restore

    dotnet = dotnet_exec_context(ctx, False, False, restore.target_framework)

    output_dir = ctx.actions.declare_directory(paths.join("publish", dotnet.config.tfm))

    args, cmd_outputs = make_builder_cmd(ctx, dotnet, "publish", restore.generated_project_file)

    inputs = depset(
        [info.output_dir, info.intermediate_dir, info.build_cache],
        transitive = [info.dep_files],
    )

    outputs = [output_dir] + cmd_outputs

    ctx.actions.run(
        mnemonic = "DotnetPublish",
        inputs = inputs,
        outputs = outputs,
        executable = dotnet.sdk.dotnet,
        arguments = [args],
        env = dotnet.env,
        tools = [dotnet.builder],
    )
    return output_dir
