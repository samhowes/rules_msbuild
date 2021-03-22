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
    library_extension = dotnetos_to_library_extension(toolchain.default_dotnetos)
    tfm = ctx.attr.target_framework
    sdk = toolchain.sdk

    references, packages, copied_files = process_deps(ctx.attr.deps, tfm)

    restore_file, restore_outputs = restore(ctx, sdk, intermediate_path, packages)

    assembly, pdb, assembly_files = _declare_assembly_files(ctx, toolchain, is_executable, library_extension, references)
    compile_file = _make_compile_file(ctx, toolchain, is_executable, output_path, restore_file, references)

    args, env, cmd_outputs = make_dotnet_cmd(ctx, sdk, "build", compile_file)

    copied_files_output = [
        ctx.actions.declare_file(cf.basename, sibling = assembly)
        for cf in copied_files
    ]

    inputs = (
        [compile_file, restore_file] +
        restore_outputs +
        ctx.files.srcs +
        copied_files +
        sdk.packs +
        sdk.shared +
        sdk.sdk_files +
        sdk.fxr +
        sdk.init_files
    )

    outputs = (
        [assembly, pdb] +
        assembly_files +
        copied_files_output +
        cmd_outputs
    )

    ctx.actions.run(
        mnemonic = "DotnetBuild",
        inputs = inputs,
        outputs = outputs,
        executable = toolchain.sdk.dotnet,
        arguments = [args],
        env = env,
    )
    return assembly, pdb, outputs + [compile_file] + restore_outputs

def _declare_assembly_files(ctx, toolchain, is_executable, library_extension, references):
    output_dir = ctx.attr.target_framework

    exe_extension = dotnetos_to_exe_extension(toolchain.default_dotnetos) if is_executable else library_extension
    assembly = ctx.actions.declare_file(paths.join(output_dir, ctx.attr.name + exe_extension))

    extensions = [
        ".deps.json",
        # already declared as `assembly` if not executable
        library_extension if is_executable else None,
    ]

    # todo(#21) toggle this when not debug
    pdb = ctx.actions.declare_file(ctx.attr.name + ".pdb", sibling = assembly)

    if is_executable:
        extensions.extend([
            ".runtimeconfig.json",
            # todo(#22) find out when this is NOT output
            ".runtimeconfig.dev.json",
        ])

    files = [
        ctx.actions.declare_file(ctx.attr.name + ext, sibling = assembly)
        for ext in extensions
        if ext != None
    ]

    return assembly, pdb, files

def _make_compile_file(ctx, toolchain, is_executable, output_path, restore_file, libraries):
    msbuild_properties = [
        element("OutputType", "Exe") if is_executable else None,
        element("OutputPath", "$(MSBuildThisFileDirectory)"),
    ]

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
    ctx.actions.expand_template(
        template = ctx.file._compile_template,
        output = compile_file,
        is_executable = False,
        substitutions = {
            "{msbuild_properties}": "\n    ".join([p for p in msbuild_properties if p != None]),
            "{imports}": inline_element("Import", {"Project": restore_file.basename}),
            "{compile_srcs}": "\n    ".join(compile_srcs),
            "{references}": "\n    ".join(references),
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
