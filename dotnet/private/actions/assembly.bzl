load("@bazel_skylib//lib:paths.bzl", "paths")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo")
load("//dotnet/private/actions:xml.bzl", "element", "inline_element")
load("//dotnet/private/actions:restore.bzl", "restore")
load(
    "//dotnet/private/actions:common.bzl",
    "INTERMEDIATE_BASE",
    "STARTUP_DIR",
    "make_dotnet_cmd",
)
load("//dotnet/private:common.bzl", "dotnetos_to_exe_extension", "dotnetos_to_library_extension")
load("//dotnet/private/actions:common.bzl", "make_dotnet_env")

def emit_assembly(ctx, is_executable):
    """Compile a dotnet assembly with the provided project template

    Args:
        ctx: a ctx with //dotnet/private/rules:common.bzl.DOTNET_ATTRS
        is_executable: if the assembly should be executable
    Returns:
        Tuple: the emitted assembly and all outputs
    """
    toolchain = ctx.toolchains["@my_rules_dotnet//dotnet:toolchain"]
    output_path = ctx.attr.target_framework
    intermediate_path = INTERMEDIATE_BASE
    tfm = ctx.attr.target_framework
    sdk = toolchain.sdk

    all_outputs = []
    msbuild_outputs = []

    references, packages, copied_files = process_deps(ctx.attr.deps, tfm)
    assembly, pdb, assembly_files, intermediate_files = _declare_assembly_files(ctx, toolchain, intermediate_path)
    msbuild_outputs += assembly_files
    msbuild_outputs += intermediate_files
    all_outputs += assembly_files
    all_outputs += intermediate_files

    for cf in copied_files:
        f = ctx.actions.declare_file(cf.basename, sibling = assembly)
        msbuild_outputs.append(f)
        all_outputs.append(f)

    restore_file, restore_outputs, cmd_outputs = restore(ctx, sdk, intermediate_path, packages)
    all_outputs += cmd_outputs

    compile_file = _make_compile_file(ctx, toolchain, is_executable, restore_file, references)

    launcher = None
    executable_files = []
    if is_executable:
        launcher, executable_files = _make_executable_files(ctx, assembly, sdk)
        msbuild_outputs += executable_files
        all_outputs += executable_files
        all_outputs.append(launcher)

    args, env, cmd_outputs = make_dotnet_cmd(ctx, sdk, "build", compile_file)
    msbuild_outputs += cmd_outputs
    all_outputs += cmd_outputs

#    msbuild_outputs.append(
#        ctx.actions.declare_directory(paths.join(intermediate_path, tfm))
#    )

    inputs = (
        [compile_file, restore_file] +
        restore_outputs +
        ctx.files.srcs +
        copied_files
    )

    print("msbuild_outputs for " + ctx.attr.name)
    for o in msbuild_outputs:
        print(o.path)

    ctx.actions.run(
        mnemonic = "DotnetBuild",
        inputs = inputs,
        outputs = msbuild_outputs,
        executable = toolchain.sdk.dotnet,
        arguments = [args],
        env = env,
    )

    return assembly, pdb, all_outputs, launcher

def _make_executable_files(ctx, assembly, sdk):
    name = ctx.attr.name

    # todo(#31) make this .bat or exe on windows
    launcher = ctx.actions.declare_file(name, sibling = assembly)

    files = [
        ctx.actions.declare_file(name + ext, sibling = assembly)
        for ext in [
            ".runtimeconfig.json",
            # todo(#22) find out when this is NOT output
            ".runtimeconfig.dev.json",
        ]
    ]

    env = [
        "export {}=\"{}\"".format(k, v)
        for k, v in make_dotnet_env(sdk).items()
    ]

    ctx.actions.expand_template(
        template = ctx.file._launcher_template,
        output = launcher,
        is_executable = True,
        substitutions = {
            "%dotnet_root%": sdk.root_file.dirname,
            "%dotnet_env%": "\n".join(env),
            "%dotnet_bin%": ctx.workspace_name + "/" + sdk.dotnet.path,
            "%target_bin%": ctx.workspace_name + "/" + assembly.short_path
        },
    )
    return launcher, files

def _declare_assembly_files(ctx, toolchain, intermediate_path):
    output_dir = ctx.attr.target_framework
    assembly = ctx.actions.declare_file(paths.join(output_dir, ctx.attr.name + ".dll"))

    # todo(#21) toggle this when not debug
    pdb = ctx.actions.declare_file(ctx.attr.name + ".pdb", sibling = assembly)
    deps = ctx.actions.declare_file(ctx.attr.name + ".deps.json", sibling = assembly)

    intermediate_files = [
        ctx.actions.declare_file(
            paths.join(intermediate_path, output_dir, ctx.attr.name + "." + ext)
        )
        for ext in [
                "AssemblyInfo.cs",
                "AssemblyInfoInputs.cache",
                "assets.cache",
                "csproj.FileListAbsolute.txt",
                "csprojAssemblyReference.cache",
                "dll",
                "pdb"
            ]
    ]
    return assembly, pdb, [assembly, pdb, deps], intermediate_files

def _make_compile_file(ctx, toolchain, is_executable, restore_file, libraries):
    msbuild_properties = [
        element("OutputPath", "$(MSBuildThisFileDirectory)"),
    ]

    if is_executable:
        msbuild_properties.extend([
            element("OutputType", "Exe"),
            element("UseAppHost", "False"),
        ])

    compile_srcs = [
        inline_element("Compile", {"Include": paths.join(STARTUP_DIR, src.path)})
        for src in depset(ctx.files.srcs).to_list()
    ]

    references = [
        element(
            "Reference",
            element(
                "HintPath",
                paths.join(STARTUP_DIR, f.path),
            ),
            {
                "Include": paths.split_extension(
                    f.basename,
                )[0],
            },
        )
        for f in libraries
    ]

    # todo(#4) add package references

    compile_file = ctx.actions.declare_file(ctx.label.name + ".csproj")
    sep = "\n    " # two indents of size 2
    ctx.actions.expand_template(
        template = ctx.file._compile_template,
        output = compile_file,
        is_executable = False,
        substitutions = {
            "{msbuild_properties}": sep.join(msbuild_properties),
            "{imports}": inline_element("Import", {"Project": restore_file.basename}),
            "{compile_srcs}": sep.join(compile_srcs),
            "{references}": sep.join(references),
        },
    )
    return compile_file

def process_deps(deps, tfm):
    """Split deps into assembly references and packages

    Args:
        deps: the deps of a dotnet assembly ctx
        tfm: the target framework moniker being built
    Returns:
        references, packages, copied_files
    """
    references = []
    packages = []
    copied_files = []

    for dep in deps:
        info = dep[DotnetLibraryInfo]
        _collect_files(info, copied_files, tfm, references, packages)

        for tinfo in info.deps.to_list():
            _collect_files(tinfo, copied_files, tfm, None, None)

    return references, packages, copied_files

def _collect_files(info, copied_files, tfm, references = None, packages = None):
    package = getattr(info, "package_info", None)
    if package != None:
        if package.is_fake:
            # todo(#20): enable JIT NuGet fetch
            fail("Package dep {} has not been fetched, did you forget to run @nuget//:fetch?".format(package.name + ":" + package.version))

        framework_info = getattr(package.frameworks, tfm, None)
        if framework_info == None:
            fail("TargetFramework {} was not fetched for package dep {}. Fetched tfms: {}. " +
                 "Did you forget to run @nuget//:fetch?".format(
                     tfm,
                     package.name + ":" + package.version,
                     ", ".join([k for k, v in package.frameworks]),
                 ))

        copied_files.extend(framework_info.assemblies + framework_info.data)
        if packages != None:
            packages.append(package)
    else:
        copied_files.append(info.assembly)
        if info.pdb != None:
            copied_files.append(info.pdb)

        if references != None:
            references.append(info.assembly)
