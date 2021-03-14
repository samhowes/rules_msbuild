load("//dotnet/private/nuget:environment.bzl", "NUGET_ENVIRONMENTS", "isolated_environment")

INTERMEDIATE_BASE = "obj"
STARTUP_DIR = "$(MSBuildStartupDirectory)"

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

def make_dotnet_env(toolchain, nuget_environment_info = None):
    dotnet_sdk_base = toolchain.sdk.root_file.dirname
    env = {
        "DOTNET_CLI_HOME": toolchain.sdk.root_file.dirname,
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        # isolate Dotnet from using the system installed sdk
        "DOTNET_MULTILEVEL_LOOKUP": "0",
        "NUGET_SHOW_STACK": "true",
    }

    os = toolchain.sdk.dotnetos
    if os not in NUGET_ENVIRONMENTS:
        fail("No nuget environment configuration for os {}".format(os))

    env_dict = NUGET_ENVIRONMENTS[os]
    if nuget_environment_info == None:
        nuget_environment_info = isolated_environment(dotnet_sdk_base)

    for name, env_name in env_dict.items():
        if env_name == "":
            continue
        env[env_name] = getattr(nuget_environment_info, name)
    print(env)
    return env

def make_dotnet_args(ctx, toolchain, target, proj, intermediate_output_dir, output_dir = None):
    args = ctx.actions.args()
    args.use_param_file("@%s")
    args.set_param_file_format("shell")
    args.add(target)

    # args.add("--nologo")
    # args.add("-p:UsePackageDownload=false")
    # args.add("--no-dependencies")  # just in case

    # args.add('--source="$(MSBuildStartupDirectory){}"'.format(toolchain.sdk.root_file.dirname)) # todo: set to @nuget
    args.add("-bl:{}".format(proj.path + ".binlog"))

    # would be no-restore, but restore creates assetts.json which the actual build depends on
    # args.add('--no-restore')
    args.add(proj.path)
    args.add("-p:IntermediateOutputPath={}/".format(intermediate_output_dir))
    if (output_dir != None):
        args.add("-p:OutputPath={}".format(output_dir))

    # GetRestoreSettingsTask#L142: this is resolved against msbuildstartupdirectory
    args.add('-p:RestoreConfigFile="{}"'.format(toolchain.sdk.nuget_build_config.path))

    return args
