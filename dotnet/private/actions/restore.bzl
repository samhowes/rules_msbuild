"""Actions for dotnet restore"""

load("@bazel_skylib//lib:paths.bzl", "paths")
load("//dotnet/private/msbuild:xml.bzl", "STARTUP_DIR", "element", "prepare_project_file")
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

    args, cmd_outputs, cmd_inputs = make_exec_cmd(ctx, dotnet, "restore", project_file, None)
    outputs.extend(cmd_outputs)

    inputs = depset(
        direct = [project_file, dotnet.sdk.config.nuget_config] + cmd_inputs,
        transitive = [dep_files.inputs, dotnet.sdk.init_files, dotnet.sdk.packs],
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
        ".dgspec.json",
        ".g.props",
        ".g.targets",
    ]

    if dotnet.sdk.major_version < 5:
        nuget_file_extensions.append(".cache")

    for ext in nuget_file_extensions:
        file_names.append(project_file.basename + ".nuget" + ext)

    file_names.extend([
        "project.assets.json",
    ])
    if dotnet.sdk.major_version >= 5:
        file_names.extend([
            "project.nuget.cache",
        ])

    if dotnet.builder != None:
        # the builder needs to interpret the paths so our output files can be moved between sandboxes, build machines,
        # etc. the next invocation will preprocess this path, and remove the ".p" extension, so MsBuild will be none
        # the wiser. Do nothing if we don't have a builder.
        for i in range(0, len(file_names)):
            f = file_names[i]

            dirname = paths.dirname(f)
            basename = paths.basename(f)

            file_names[i] = paths.join(paths.join(
                dirname,
                dotnet.builder_output_dir,
                basename,
            ))

    files = [
        ctx.actions.declare_file(paths.join(dotnet.config.intermediate_path, file_name))
        for file_name in file_names
    ]

    return files
