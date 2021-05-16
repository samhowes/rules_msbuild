load("@bazel_skylib//lib:paths.bzl", "paths")
load("//dotnet/private:context.bzl", "dotnet_exec_context", "make_exec_cmd")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo")

def publish(ctx):
    info = ctx.attr.target[DotnetLibraryInfo]

    dotnet = dotnet_exec_context(ctx, False, False, info.target_framework)

    output_dir = ctx.actions.declare_directory(paths.join("publish", dotnet.config.output_dir_name))
    files = struct(
        output_dir = output_dir,
        content = info.content,
        data = info.data,
    )

    args, cmd_outputs, cmd_inputs, _ = make_exec_cmd(ctx, dotnet, "publish", info.project_file, files)

    groups = ctx.attr.target[OutputGroupInfo]
    ctx.actions.run(
        mnemonic = "DotnetPublish",
        inputs = depset(
            [info.project_file, info.intermediate_dir] + cmd_inputs,
            transitive = [groups.all, dotnet.sdk.init_files, files.data, files.content],
        ),
        outputs = [output_dir] + cmd_outputs,
        executable = dotnet.sdk.dotnet,
        arguments = [args],
        env = dotnet.env,
        tools = dotnet.tools,
    )

    info = DefaultInfo(
        files = depset([output_dir]),
    )
    return [info]
