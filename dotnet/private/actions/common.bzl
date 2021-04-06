load("//dotnet/private/msbuild:environment.bzl", "NUGET_ENVIRONMENTS", "isolated_environment")
load("@bazel_skylib//lib:paths.bzl", "paths")

INTERMEDIATE_BASE = "obj"

def built_path(ctx, outputs, p, is_directory = False):
    if is_directory:
        msbuild_path = p + "/"
        output = ctx.actions.declare_directory(p)
    else:
        output = ctx.actions.declare_file(p)
        msbuild_path = p
    outputs.append(output)
    return struct(
        file = output,
        msbuild_path = msbuild_path,
        short_path = output.short_path,
    )

def make_dotnet_exec_cmd(ctx, sdk, msbuild_target, proj):
    """Create a command for use during the execution phase"""
    binlog = False  # todo(#51) disable when not debugging the build
    if True:
        binlog = True

    arg_list, env, binlog_path = make_dotnet_cmd(
        sdk.root_file.dirname,
        sdk.dotnetos,
        proj.path,
        msbuild_target,
        binlog,
    )

    outputs = []
    if binlog_path != None:
        outputs.append(ctx.actions.declare_file(paths.basename(binlog_path)))

    args = ctx.actions.args()
    for arg in arg_list:
        args.add(arg)
    return args, env, outputs

def make_dotnet_cmd(dotnet_sdk_root, os, project_path, msbuild_target, binlog = False):
    args, binlog_path = make_dotnet_args(msbuild_target, project_path, binlog)
    env = make_dotnet_env(dotnet_sdk_root, os)

    return args, env, binlog_path

def make_dotnet_env(dotnet_sdk_root, os):
    env = {
        "DOTNET_CLI_HOME": dotnet_sdk_root,
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        # isolate Dotnet from using the system installed sdk
        "DOTNET_MULTILEVEL_LOOKUP": "0",
        "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
        "DOTNET_NOLOGO": "1",
        "NUGET_SHOW_STACK": "true",
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

def make_dotnet_args(msbuild_target, project_path, binlog):
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
