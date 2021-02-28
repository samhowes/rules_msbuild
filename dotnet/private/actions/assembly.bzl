load("//dotnet/private/actions:common.bzl", "built_path")
load("//dotnet/private:common.bzl", "dotnetos_to_library_extension", "dotnetos_to_exe_extension")

def emit_assembly(ctx, is_executable):
    """Compile a dotnet assembly with the provided project template
    
    Args:
        ctx: a ctx with //dotnet/private/rules:common.bzl.DOTNET_ATTRS
        is_executable: if the assembly should be executable
    Returns:
        Tuple: the emitted assembly and all outputs
    """
    toolchain = ctx.toolchains["@my_rules_dotnet//dotnet:toolchain"]
    tfm = ctx.attr.target_framework
    dotnetos = toolchain.default_dotnetos
    outputs = []
    output_dir = tfm

    library_extension = dotnetos_to_library_extension(dotnetos)
    extension = dotnetos_to_exe_extension(dotnetos) if is_executable else library_extension
    assembly =  built_path(ctx, outputs, output_dir + "/" + ctx.label.name + extension)
    
    intermediate_output_dir = _declare_output_files(ctx, tfm, outputs, is_executable, library_extension)
    proj = _make_project_file(ctx,toolchain, is_executable, tfm, outputs)
    args = _make_dotnet_args(ctx, toolchain, proj, intermediate_output_dir, output_dir)
    env = _make_dotnet_env(toolchain)

    sdk = toolchain.sdk
    ctx.actions.run(
        mnemonic = "DotnetBuild",
        inputs = (
            ctx.files.srcs + 
            [proj]
            + sdk.packs + sdk.shared + sdk.sdk_files
            + sdk.fxr),
        outputs = outputs,
        executable = toolchain.sdk.dotnet,
        arguments = [args],
        env = env,
    )

    return assembly, outputs
    
def _declare_output_files(ctx, tfm, outputs, is_executable, library_extension): 
    intermediate_base = built_path(ctx, outputs, "obj", True)
    intermediate_output = built_path(ctx, outputs, intermediate_base.msbuild_path + tfm, True)
    
    output_dir = tfm
    output_extensions = [
        ".deps.json",
        ".pdb", # todo toggle this somehow
    ]

    if is_executable:
        output_extensions.extend([
            library_extension, # already declared before this method if it is not executable
            ".runtimeconfig.dev.json", # todo find out when this is NOT output
            ".runtimeconfig.json",
        ])

    for ext in output_extensions: 
        built_path(ctx, outputs, output_dir + "/" + ctx.label.name + ext)
    return intermediate_output

def _make_project_file(ctx, toolchain, is_executable, tfm, outputs): 
    # intermediate file, not an output, don't add to outputs
    proj = ctx.actions.declare_file(ctx.label.name + ".csproj")    
    
    output_type = _build_property("OutputType", "Exe") if is_executable else ""

    compile_srcs = [
        '    <Compile Include="$(MSBuildStartupDirectory)/{}" />'.format(src.path)
        for src in depset(ctx.files.srcs).to_list()
    ]

    msbuild_properties = [
        _build_property("RestoreSources", "$(MSBuildStartupDirectory)/"+toolchain.sdk.root_file.dirname)
    ]

    ctx.actions.expand_template(
        template = ctx.file._proj_template,
        output = proj,
        is_executable = False,
        substitutions = {
            "{compile_srcs}" : "\n".join(compile_srcs),
            "{tfm}": tfm,
            "{output_type}": output_type,
            "{msbuild_properties}": "\n".join(msbuild_properties)
        },
    )
    return proj

def _build_property(name, value):
    return "\n    <{name}>{value}</{name}>".format(name=name, value=value)

def _make_dotnet_args(ctx, toolchain, proj, intermediate_output_dir, output_dir): 
    args = ctx.actions.args()
    args.use_param_file("@%s")
    args.set_param_file_format("shell")
    args.add('build')
    args.add('--nologo')
    args.add('-p:UsePackageDownload=false')
    args.add('--no-dependencies') # just in case
    # args.add('--source="$(MSBuildStartupDirectory){}"'.format(toolchain.sdk.root_file.dirname)) # todo: set to @nuget
    args.add('-bl:{}'.format(proj.path + '.binlog'))
    # would be no-restore, but restore creates assetts.json which the actual build depends on
    # args.add('--no-restore') 
    args.add(proj.path)
    args.add('-p:IntermediateOutputPath={}'.format(intermediate_output_dir.msbuild_path))
    args.add('-p:OutputPath={}'.format(output_dir))

    # GetRestoreSettingsTask#L142: this is resolved against msbuildstartupdirectory
    args.add('-p:RestoreConfigFile="{}"'.format(toolchain.sdk.nuget_build_config.path)) 

    return args

def _make_dotnet_env(toolchain): 
    dotnet_sdk_base = toolchain.sdk.root_file.dirname
    env = {
        "DOTNET_CLI_HOME": toolchain.sdk.root_file.dirname,
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        # isolate Dotnet from using the system installed sdk
        "DOTNET_MULTILEVEL_LOOKUP": "0",

         # https://github.com/NuGet/NuGet.Client/blob/3d1d3c77f441e2f653dad789e28fa11fec189b87/src/NuGet.Core/NuGet.Common/PathUtil/NuGetEnvironment.cs#L10
        "PROGRAMDATA": dotnet_sdk_base,
        "USERPROFILE": dotnet_sdk_base,
        "PROGRAMFILES(X86)": dotnet_sdk_base, # used when constructing XPlatMachineWideSetting
        "PROGRAMFILES": dotnet_sdk_base, # used when constructing XPlatMachineWideSetting
        #todo add variables for other platforms
        "APPDATA": dotnet_sdk_base # used by Settings.cs#LoadUserSpecificSettings: `NuGetFolderPath.UserSettingsDirectory` (windows only)
    }
    return env
    
