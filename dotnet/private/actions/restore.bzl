"""Actions for dotnet restore"""

load("@bazel_skylib//lib:paths.bzl", "paths")
load("//dotnet/private/msbuild:xml.bzl", "STARTUP_DIR", "prepare_restore_file")
load("//dotnet/private:context.bzl", "make_exec_cmd")
load("//dotnet/private:providers.bzl", "DEFAULT_SDK")

def restore(ctx, dotnet, sdk, intermediate_path, packages):
    """Emits an action for generating files necessary for a nuget restore

    https://docs.microsoft.com/en-us/nuget/concepts/package-installation-process

    Args:
        ctx: the ctx of the dotnet rule
        sdk: the dotnet sdk
        intermediate_path: the path to the obj directory
        packages: a list of NuGetPackageInfo providers to restore
    Returns:
        a list of files in the package
    """
    restore_file = _make_restore_file(ctx, sdk, intermediate_path, packages)

    outputs = _declare_files(dotnet, ctx, restore_file, intermediate_path)

    args, cmd_outputs = make_exec_cmd(dotnet, ctx, "restore", restore_file, intermediate_path)
    outputs.extend(cmd_outputs)

    restore_inputs = []
    for p in packages:
        restore_inputs.extend(p.all_files.to_list())

    ctx.actions.run(
        mnemonic = "NuGetRestore",
        inputs = (
            [restore_file] + restore_inputs +
            sdk.init_files +
            [sdk.config.nuget_config]
        ),
        outputs = outputs,
        executable = sdk.dotnet,
        arguments = [args],
        env = dotnet.env,
        tools = dotnet.tools,
    )

    return restore_file, outputs + [restore_file], cmd_outputs

def _declare_files(dotnet, ctx, restore_file, intermediate_path):
    file_names = []

    nuget_file_extensions = [
        ".cache",
        ".dgspec.json",
        ".g.props",
        ".g.targets",
    ]
    for ext in nuget_file_extensions:
        file_names.append(restore_file.basename + ".nuget" + ext)

    file_names.extend([
        "project.assets.json",
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
        ctx.actions.declare_file(paths.join(intermediate_path, file_name))
        for file_name in file_names
    ]

    return files

def _make_restore_file(ctx, sdk, intermediate_path, packages):
    build_sdk = DEFAULT_SDK  # todo(#3)
    substitutions = prepare_restore_file(
        build_sdk,
        intermediate_path,
        [],  # no references needed for the restore
        packages,
        paths.join(STARTUP_DIR, sdk.config.nuget_config.path),
        ctx.attr.target_framework,
    )

    restore_file = ctx.actions.declare_file(ctx.attr.name + ".restore.proj")
    ctx.actions.expand_template(
        template = ctx.file._restore_template,
        output = restore_file,
        is_executable = False,
        substitutions = substitutions,
    )
    return restore_file
