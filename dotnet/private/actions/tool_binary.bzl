load("@bazel_skylib//lib:paths.bzl", "paths")
load("@bazel_skylib//lib:dicts.bzl", "dicts")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo")
load(":common.bzl", "add_diagnostics", "get_nuget_files")

def build_tool_binary(ctx, dotnet):
    """Create a binary used for the dotnet toolchain itself.

    This implementation assumes that no other targets will depend on this binary for anything other than executing as a
    tool. Since this is part of the toolchain itself, it can't execute a multiphase restore, build, publish,
    because bazel's sandboxing/remote execution will cause msbuild to not be able to find reference paths between
    actions. So instead, we execute all msbuild steps in a single action via invoking the publish target directly.
    """
    output_dir = ctx.actions.declare_directory("publish")
    dep_files = _process_deps(ctx, dotnet)
    assembly = ctx.actions.declare_file(paths.join(output_dir.short_path, ctx.attr.name + ".dll"))

    args = ctx.actions.args()
    args.add_all([
        "publish",
        ctx.file.project_file,
        "-p:RestoreConfigFile=" + dotnet.sdk.config.nuget_config.path,
        "-nologo",
        "-bl",
    ])

    inputs = depset(
        ctx.files.srcs + [ctx.file.project_file, dotnet.sdk.config.nuget_config] + ctx.files._bazel_packages,
        transitive = [dep_files, dotnet.sdk.runfiles],
    )
    outputs = [output_dir, assembly]

    binlog = add_diagnostics(ctx, dotnet, outputs)
    if binlog != None:
        args.add("-bl:" + binlog.path)

    ctx.actions.run(
        mnemonic = "DotnetBuild",
        inputs = inputs,
        outputs = outputs,
        executable = dotnet.sdk.dotnet,
        arguments = [args],
        env = dicts.add(dotnet.env, {
            "BazelBuild": "true",
            "BINDIR": ctx.bin_dir.path,
            "_IsBootstrapping": "true",
        }),
    )
    return DotnetLibraryInfo(
        assembly = assembly,
        output_dir = output_dir,
    ), outputs

def _process_deps(ctx, dotnet):
    transitive = []
    for d in ctx.attr.deps:
        get_nuget_files(d, dotnet.config.tfm, transitive)
    return depset(transitive = transitive)
