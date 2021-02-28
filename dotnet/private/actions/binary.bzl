"""See dotnet/toolchains.md#binary for full documentation."""

def _built_path(ctx, outputs, p, is_directory=False):
    if is_directory:
        msbuild_path = p + "/"
        output = ctx.actions.declare_directory(p)        
    else:
        output = ctx.actions.declare_file(p)
        msbuild_path = p
    outputs.append(output)
    return struct(
        output = output,
        msbuild_path = msbuild_path,
        short_path = output.short_path
    )

def emit_binary(dotnet):
    """See dotnet/toolchains.md#binary for full documentation."""

    ctx = dotnet._ctx
    tfm = ctx.attr.target_framework
    outputs = []
    intermediate_base = _built_path(ctx, outputs, "obj", True)
    intermediate_output = _built_path(ctx, outputs, intermediate_base.msbuild_path + "/" + tfm, True)

    # todo: declare intermediate files in obj folder

    # output_dir = _built_path(ctx, outputs, tfm, True)
    output_dir = tfm
    output_extensions = [
        "deps.json",
        "dll",
        "pdb", # todo toggle this somehow
        "runtimeconfig.dev.json", # todo find out when this is NOT output
        "runtimeconfig.json"
    ]

    name = dotnet._ctx.label.name
    executable =  _built_path(ctx, outputs, output_dir + "/" + name + dotnet.exe_extension)
    for ext in output_extensions: 
        _built_path(ctx, outputs, output_dir + "/" + name + "." + ext)

    #todo specify these
    # runfiles = dotnet._ctx.runfiles(files = [])
    symlink_target = "./" + ctx.label.package
        
    src_symlink = ctx.actions.declare_symlink("src")
    ctx.actions.symlink(output=src_symlink, target_path= symlink_target)
    # intermediate file, not an output?
    proj = dotnet.actions.declare_file(dotnet._ctx.label.name + ".csproj")
    
    pkg_len = len(ctx.label.package)
    compile_srcs = [
        '    <Compile Include="src{}" />'.format(src.path[pkg_len:])
        for src in depset(ctx.files.srcs).to_list()
    ]

    dotnet.actions.expand_template(
        template = dotnet._ctx.file._proj_template,
        output = proj,
        is_executable = False,
        substitutions = {
            "{compile_srcs}" : "\n".join(compile_srcs),
            "{tfm}": tfm
        },
    )

    args = dotnet.actions.args()
    args.use_param_file("@%s")
    args.set_param_file_format("shell")
    args.add('build')
    args.add('--nologo')
    args.add('-p:UsePackageDownload=false')
    args.add('--no-dependencies') # just in case
    args.add('--source="{}"'.format(dotnet.toolchain.sdk.root_file.dirname)) # todo: set to @nuget
    args.add('-bl:{}'.format(proj.path + '.binlog'))
    # would be no-restore, but restore creates assetts.json which the actual build depends on
    # args.add('--no-restore') 
    args.add(proj.path)
    args.add('-p:IntermediateOutputPath={}'.format(intermediate_output.msbuild_path))
    args.add('-p:OutputPath={}'.format(output_dir))

    env = {
        "DOTNET_CLI_HOME": dotnet.toolchain.sdk.root_file.dirname,
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        # isolate Dotnet from using the system installed sdk
        "DOTNET_MULTILEVEL_LOOKUP": "0",
    }

    _add_nuget_environment(dotnet, env)

    sdk = dotnet.toolchain.sdk
    dotnet.actions.run(
        mnemonic = "DotnetBuild",
        inputs = (
            dotnet._ctx.files.srcs + 
            [proj, src_symlink]
            + sdk.packs + sdk.shared + sdk.sdk_files
            + sdk.fxr),
        outputs = outputs,
        executable = dotnet.toolchain.sdk.dotnet,
        arguments = [args],
        env = env,
    )

    return executable.output, outputs

def _add_nuget_environment(dotnet, env):
    """ Adds nuget environment variables for package restore

    https://github.com/NuGet/NuGet.Client/blob/3d1d3c77f441e2f653dad789e28fa11fec189b87/src/NuGet.Core/NuGet.Common/PathUtil/NuGetEnvironment.cs#L10
    Args:
        dotnet: dotnet_context() result
        env: a dict of environment variables
    """

    dotnet_sdk_base = dotnet.toolchain.sdk.root_file.dirname
    env.update({
        "PROGRAMDATA": dotnet_sdk_base,
        "USERPROFILE": dotnet_sdk_base,
        "PROGRAMFILES(X86)": dotnet_sdk_base, # used when constructing XPlatMachineWideSetting
        "PROGRAMFILES": dotnet_sdk_base, # used when constructing XPlatMachineWideSetting
        #todo add variables for other platforms
        "APPDATA": dotnet_sdk_base # used by Settings.cs#LoadUserSpecificSettings: `NuGetFolderPath.UserSettingsDirectory` (windows only)
    })

