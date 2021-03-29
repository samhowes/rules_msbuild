"""Actions for dotnet restore"""

load("@bazel_skylib//lib:paths.bzl", "paths")
load(
    "//dotnet/private/actions:xml.bzl",
    "STARTUP_DIR",
    "THIS_DIR",
    "inline_element",
    "properties",
)
load(
    "//dotnet/private/actions:common.bzl",
    "make_dotnet_cmd",
)

def restore(ctx, sdk, intermediate_path, packages):
    """Emits an action for generating files necessary for a nuget restore

    Args:
        ctx: the ctx of the dotnet rule
        sdk: the dotnet sdk
        intermediate_path: the path to the obj directory
        packages: a list of NuGetPackageInfo providers to restore
    Returns:
        a list of files in the package
    """
    if len(packages) > 0:
        fail("fial")
    restore_file = _make_restore_file(ctx, sdk, intermediate_path, packages)

    outputs = _declare_files(ctx, restore_file, intermediate_path)

    args, env, cmd_outputs = make_dotnet_cmd(ctx, sdk, "restore", restore_file)
    outputs.extend(cmd_outputs)

    ctx.actions.run(
        mnemonic = "NuGetRestore",
        inputs = (
            [restore_file] +  # todo: maybe include NuSpec files as inputs?
            sdk.init_files +
            [sdk.nuget_build_config]
        ),
        outputs = outputs,
        executable = sdk.dotnet,
        arguments = [args],
        env = env,
    )

    return restore_file, outputs + [restore_file], cmd_outputs

def _declare_files(ctx, restore_file, intermediate_path):
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

    files = [
        ctx.actions.declare_file(paths.join(intermediate_path, file_name))
        for file_name in file_names
    ]

    return files

def _make_restore_file(ctx, sdk, intermediate_path, packages):
    pre_import_msbuild_properties = {
        "RestoreSources": paths.join(STARTUP_DIR, sdk.root_file.dirname),
        # this is where nuget creates project.assets.json (and other files) during a restore
        "BaseIntermediateOutputPath": THIS_DIR + intermediate_path,
        "IntermediateOutputPath": paths.join(THIS_DIR, intermediate_path),
        # https://docs.microsoft.com/en-us/visualstudio/msbuild/customize-your-build?view=vs-2019#msbuildprojectextensionspath
        # this is where nuget looks for a project.assets.json during a build
        "MSBuildProjectExtensionsPath": THIS_DIR + intermediate_path,
        # we could just set ProjectAssetsFile here, but we're setting the other properties in case they have other impacts
        "OutputPath": THIS_DIR + paths.dirname(intermediate_path),
        "ImportDirectoryBuildProps": "false",
        "UseSharedCompilation": "false",
    }

    package_references = [
        inline_element(
            "PackageReference",
            {
                "Include": p.name,
                "Version": p.version,
            },
        )
        for p in packages
    ]

    restore_file = ctx.actions.declare_file(ctx.attr.name + ".restore.proj")
    ctx.actions.expand_template(
        template = ctx.file._restore_template,
        output = restore_file,
        is_executable = False,
        substitutions = {
            "{pre_import_msbuild_properties}": properties(pre_import_msbuild_properties),
            "{sdk}": "Microsoft.NET.Sdk",  # todo(#3)
            "{tfm}": ctx.attr.target_framework,
            "{package_references}": "\n    ".join(package_references),
        },
    )
    return restore_file
