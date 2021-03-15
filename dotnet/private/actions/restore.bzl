"""Actions for dotnet restore"""

load("@bazel_skylib//lib:paths.bzl", "paths")
load("//dotnet/private/actions:xml.bzl", "element", "inline_element")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo")
load(
    "//dotnet/private/actions:common.bzl",
    "STARTUP_DIR",
    "make_dotnet_args",
    "make_dotnet_env",
)

def restore(ctx, intermediate_path):
    """Emits an action for generating files necessary for a nuget restore
    
    Args:
        ctx: the ctx of the dotnet rule
        intermediate_path: the path to the obj directory
    Returns:
        a list of files in the package
    """
    toolchain = ctx.toolchains["@my_rules_dotnet//dotnet:toolchain"]
    restore_file = _make_restore_file(ctx, toolchain, intermediate_path)

    outputs = _declare_files(ctx, restore_file, intermediate_path)

    args = make_dotnet_args(ctx, toolchain, "restore", restore_file)
    env = make_dotnet_env(toolchain)

    ctx.actions.run(
        mnemonic = "NuGetRestore",
        inputs = (
            [restore_file]  # todo: maybe include NuSpec files as inputs?
        ),
        outputs = outputs,
        executable = toolchain.sdk.dotnet,
        arguments = [args],
        env = env,
    )

    return restore_file, outputs

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

def _make_restore_file(ctx, toolchain, intermediate_path):
    packages = [
        p[DotnetLibraryInfo]
        for p in ctx.attr.deps
        if hasattr(p[DotnetLibraryInfo], "package_info") and p[DotnetLibraryInfo].package_info != None
    ]

    package_references = [
        inline_element(
            "PackageReference",
            {
                "Include": p.package_info.name,
                "Version": p.package_info.version,
            },
        )
        for p in packages
    ]

    msbuild_properties = [
        element("RestoreSources", paths.join(STARTUP_DIR, toolchain.sdk.root_file.dirname)),
        element("IntermediateOutputPath", "$(MSBuildThisFileDirectory)" + intermediate_path),
    ]

    restore_file = ctx.actions.declare_file(ctx.attr.name + ".restore.proj")
    ctx.actions.expand_template(
        template = ctx.file._restore_template,
        output = restore_file,
        is_executable = False,
        substitutions = {
            "{tfm}": ctx.attr.target_framework,
            "{package_references}": "\n    ".join(package_references),
            "{msbuild_properties}": "\n    ".join(msbuild_properties),
        },
    )
    return restore_file
