"""Actions for dotnet restore"""

load("@bazel_skylib//lib:paths.bzl", "paths")
load("//dotnet/private/actions:xml.bzl", "element", "inline_element")
load(
    "//dotnet/private/actions:common.bzl",
    "INTERMEDIATE_BASE",
    "STARTUP_DIR",
    "make_dotnet_args",
    "make_dotnet_env",
)
load("//dotnet/private:providers.bzl", "NugetPreRestoreInfo")

def restore(ctx):
    """Emits an action for generating files necessary for a nuget restore
    
    Args:
        ctx: the ctx of the nuget_restore rule
    Returns:
        a list of files in the package
    """
    toolchain = ctx.toolchains["@my_rules_dotnet//dotnet:toolchain"]
    pre_restores = [
        d[NugetPreRestoreInfo]
        for d in ctx.attr.deps
    ]

    print("restore")
    project_references = []
    for d in pre_restores:
        print(d.props_file.path)

        # outputs.extend(_declare_intermediate_files(ctx, d.props_file.short_path, d.primary_name))
        project_references.append(
            inline_element("ProjectReference", {"Include": paths.join(STARTUP_DIR, d.props_file.path)}),
        )

    msbuild_properties = [
        element("RestoreSources", paths.join(STARTUP_DIR, toolchain.sdk.root_file.dirname)),
    ]

    proj = ctx.actions.declare_file(paths.join(ctx.attr.name, "restore.proj"))
    outputs = [ctx.actions.declare_file(
        "packages.lock.json",
        sibling = proj,
    )]

    ctx.actions.expand_template(
        template = ctx.file._restore_template,
        output = proj,
        is_executable = False,
        substitutions = {
            "{tfms}": ";".join(ctx.attr.target_frameworks),
            "{references}": "\n    ".join(project_references),
            "{msbuild_properties}": "\n    ".join([]),
        },
    )

    args = make_dotnet_args(ctx, toolchain, "restore", proj, paths.join(ctx.attr.name, INTERMEDIATE_BASE))
    env = make_dotnet_env(toolchain)

    sdk = toolchain.sdk
    ctx.actions.run(
        mnemonic = "NuGetRestore",
        inputs = (
            [d.props_file for d in pre_restores] +
            [proj]
        ),
        outputs = outputs,
        executable = toolchain.sdk.dotnet,
        arguments = [args],
        env = env,
    )

    return outputs

def _declare_intermediate_files(ctx, primary_path, primary_name):
    prefix = primary_name

    intermediate_names = []

    top_files = [
        ".csproj.nuget.dgspec.json",
        ".csproj.nuget.g.props",
        ".csproj.nuget.g.targets",
    ]
    for f in top_files:
        name = prefix + f
        intermediate_names.append(name)

    intermediate_names.extend([
        "project.assets.json",
        "project.nuget.cache",
    ])

    intermediate_files = [
        ctx.actions.declare_file(paths.join(primary_path, INTERMEDIATE_BASE, f))
        for f in intermediate_names
    ]

    return intermediate_files
