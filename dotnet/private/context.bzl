"""This module creates a `DotnetContext` struct. Instances of this object are referred to as `dotnet`.

A DotnetContext is the "context" in which we are interacting with the dotnet binary. This is necessary because there are
a couple different contexts in which we are interacting with the dotnet binary, but in these different contexts, we
still want to perform the same fundamental actions with the dotnet binary.

1) The "primary" context that we'll be interacting with dotnet in, is when we are performing the steps to actually build
    an assembly for a user that wrote a `dotnet_binary` rule in a BUILD file. This context will not be invoking
    `dotnet build` directly, but instead passing build instructions to //dotnet/tools/builder.

    The "builder" takes care of a couple administrative things to de-bazilfy the inputs to dotnet, and to bazelify
    dotnet's output, before invoking dotnet build directly.

2) "Building the builder" context. Before we can enter the "primary" context we have to build the builder that assists
    with the primary context. This context invokes dotnet directly to compile the builder sources into the builder dll.
    This context cannot depend on the toolchain, because the toolchain depends on this context.

3) "NuGet Fetch" context. NuGet fetch happens in the loading phase, not the execution phase, as such, we don't have
    access to the toolchain and resolved labels from the build file, because those are constructed for the execution
    phase. In this context, we execute dotnet with repository_ctx.execute(), which takes in a list of strings as
    arguments, the other contexts need an "args" object produced by ctx.actions.args()
"""

load("//dotnet/private/msbuild:environment.bzl", "NUGET_ENVIRONMENTS", "isolated_environment")
load("//dotnet/private/msbuild:xml.bzl", "INTERMEDIATE_BASE")
load("@bazel_skylib//lib:paths.bzl", "paths")

def dotnet_context(sdk_root, os, builder = None, sdk = None):
    ext = ".exe" if os == "windows" else ""
    return struct(
        path = paths.join(sdk_root, "dotnet" + ext),
        env = _make_env(sdk_root, os),
        builder = builder,
        builder_output_dir = "processed",
        sdk = sdk,
        tools = [builder] if builder != None else [],
        ext = ext,
    )

def _make_env(dotnet_sdk_root, os):
    env = {
        "DOTNET_CLI_HOME": dotnet_sdk_root,
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        # isolate Dotnet from using the system installed sdk
        "DOTNET_MULTILEVEL_LOOKUP": "0",
        "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
        "DOTNET_NOLOGO": "1",
        "NUGET_SHOW_STACK": "true",
        # "BUILDER_DEBUG": "1",
    }

    if os not in NUGET_ENVIRONMENTS:
        fail("No nuget environment configuration for os {}".format(os))

    env_dict = NUGET_ENVIRONMENTS[os]
    nuget_environment_info = isolated_environment(dotnet_sdk_root)

    for name, env_name in env_dict.items():
        if env_name == "":
            continue
        env[env_name] = getattr(nuget_environment_info, name)

    return env

def make_exec_cmd(dotnet, ctx, msbuild_target, proj, intermediate_path):
    """Create a command for use during the execution phase"""
    binlog = False  # todo(#51) disable when not debugging the build
    if True:
        binlog = True

    arg_list, binlog_path = make_cmd(
        dotnet,
        proj.path,
        msbuild_target,
        binlog,
    )

    outputs = []
    if binlog_path != None:
        outputs.append(ctx.actions.declare_file(paths.basename(binlog_path)))

    args = ctx.actions.args()
    if dotnet.builder != None:
        intermediate_path_full = paths.join(str(proj.dirname), intermediate_path)
        processed_path = paths.join(intermediate_path_full, dotnet.builder_output_dir)
        args.add(dotnet.builder)
        args.add(msbuild_target)
        args.add(intermediate_path_full)
        args.add(processed_path)
        args.add(dotnet.sdk.config.trim_path)
        args.add("--")
        args.add(dotnet.path)

    for arg in arg_list:
        args.add(arg)
    return args, outputs

def make_cmd(dotnet, project_path, msbuild_target, binlog = False):
    args_list = [
        "msbuild",
        "-t:" + msbuild_target,
        "-nologo",
    ]

    args_list.append(project_path)

    binlog_path = None
    if binlog:
        binlog_path = project_path + ".binlog"
        args_list.append("-bl:{}".format(binlog_path))

    # todo
    # if msbuild_target != "restore":
    #     args.add("--no-restore")

    return args_list, binlog_path
