load("//dotnet/private/nuget:environment.bzl", "NUGET_ENVIRONMENTS", "isolated_environment")
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

def make_dotnet_cmd(ctx, sdk, msbuild_target, proj, nuget_environment_info = None):
    args, outputs = make_dotnet_args(ctx, sdk, msbuild_target, proj)
    env = make_dotnet_env(sdk, nuget_environment_info, ctx.var["COMPILATION_MODE"])

    return args, env, outputs

def make_dotnet_env(sdk, nuget_environment_info = None, prefix=None):
    dotnet_sdk_base = sdk.root_file.dirname
    env = {
        "DOTNET_CLI_HOME": sdk.root_file.dirname,
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        # isolate Dotnet from using the system installed sdk
        "DOTNET_MULTILEVEL_LOOKUP": "0",
        "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
        "DOTNET_NOLOGO": "1",
        "NUGET_SHOW_STACK": "true",
        "PREFIX":prefix
    }

    os = sdk.dotnetos
    if os not in NUGET_ENVIRONMENTS:
        fail("No nuget environment configuration for os {}".format(os))

    env_dict = NUGET_ENVIRONMENTS[os]
    if nuget_environment_info == None:
        nuget_environment_info = isolated_environment(dotnet_sdk_base)

    for name, env_name in env_dict.items():
        if env_name == "":
            continue
        env[env_name] = getattr(nuget_environment_info, name)

    return env

def make_dotnet_args(ctx, sdk, msbuild_target, proj):
    args = ctx.actions.args()
#    args.use_param_file("@%s.rsp", use_always=True)
#    args.set_param_file_format("shell")
    args.add("msbuild")
    args.add("-t:" + msbuild_target)
    args.add(proj.path)
    args.add("-nologo")

    outputs = []

    # todo disable when not debugging the build
    if True:
        binlog = ctx.actions.declare_file(proj.basename + ".binlog", sibling = proj)
        args.add("-bl:{}".format(binlog.path))
        outputs.append(binlog)

    # todo
    # if msbuild_target != "restore":
    #     args.add("--no-restore")

    # GetRestoreSettingsTask#L142: this is resolved against msbuildstartupdirectory
    args.add('-p:RestoreConfigFile="{}"'.format(sdk.nuget_build_config.path))

    return args, outputs
