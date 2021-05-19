"""Actions for dotnet restore"""

load("@bazel_skylib//lib:paths.bzl", "paths")
load("//dotnet/private/msbuild:xml.bzl", "element", "prepare_project_file")
load("//dotnet/private:context.bzl", "make_exec_cmd")
load("//dotnet/private:providers.bzl", "DEFAULT_SDK")

def restore(ctx, dotnet, project_file, dep_files):
    """Emits an action for generating files necessary for a nuget restore

    https://docs.microsoft.com/en-us/nuget/concepts/package-installation-process

    Args:
        ctx: the ctx of the dotnet rule
        sdk: the dotnet sdk
        packages: a list of NuGetPackageInfo providers to restore
    Returns:
        a list of files in the package
    """
    outputs = _declare_files(ctx, dotnet, project_file)

    args, cmd_outputs, cmd_inputs, _ = make_exec_cmd(ctx, dotnet, "restore", project_file, None)
    outputs.extend(cmd_outputs)

    inputs = depset(
        direct = [project_file, dotnet.sdk.config.nuget_config] + cmd_inputs,
        transitive = [dep_files.restore, dotnet.sdk.init_files, dotnet.sdk.packs],
    )

    ctx.actions.run(
        mnemonic = "NuGetRestore",
        inputs = inputs,
        outputs = outputs,
        executable = dotnet.sdk.dotnet,
        arguments = [args],
        env = dotnet.env,
        tools = dotnet.tools,
    )

    return outputs

def _declare_files(ctx, dotnet, project_file):
    file_names = []

    nuget_file_extensions = [
        ".g.props",
        ".g.targets",
    ]

    for ext in nuget_file_extensions:
        file_names.append(project_file.basename + ".nuget" + ext)

    file_names.extend([
        "project.assets.json",
    ])

    files = [
        ctx.actions.declare_file(paths.join(dotnet.config.intermediate_path, file_name))
        for file_name in file_names
    ]

    return files
