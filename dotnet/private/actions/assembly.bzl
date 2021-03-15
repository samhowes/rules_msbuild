load("@bazel_skylib//lib:paths.bzl", "paths")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo")
load("//dotnet/private/actions:xml.bzl", "element", "inline_element")
load("//dotnet/private/actions:restore.bzl", "restore")
load(
    "//dotnet/private/actions:common.bzl",
    "INTERMEDIATE_BASE",
    "STARTUP_DIR",
    "make_dotnet_args",
    "make_dotnet_env",
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

    library_infos = depset(
        direct = [dep[DotnetLibraryInfo] for dep in ctx.attr.deps],
        transitive = [dep[DotnetLibraryInfo].deps for dep in ctx.attr.deps],
    )

    restore_file, restore_outputs = restore(ctx, intermediate_path)

    assembly, pdb, assembly_files = _declare_assembly_files(ctx, toolchain, is_executable, library_extension, library_infos)
    compile_file = _make_compile_file(ctx, toolchain, is_executable, output_path, restore_file, library_infos)

    args = make_dotnet_args(ctx, toolchain, "build", compile_file)
    env = make_dotnet_env(toolchain)

    dep_files = []
    for li in library_infos.to_list():
        dep_files.extend([li.assembly, li.pdb])

    copied_dep_files = [
        ctx.actions.declare_file(df.basename, sibling = assembly)
        for df in dep_files
    ]

    sdk = toolchain.sdk
    inputs = (
        [compile_file, restore_file] +
        restore_outputs +
        ctx.files.srcs +
        dep_files +
        sdk.packs +
        sdk.shared +
        sdk.sdk_files +
        sdk.fxr
    )

    outputs = (
        [assembly, pdb] +
        assembly_files +
        copied_dep_files
    )

    ctx.actions.run(
        mnemonic = "DotnetBuild",
        inputs = inputs,
        outputs = outputs,
        executable = toolchain.sdk.dotnet,
        arguments = [args],
        env = env,
    )
    return assembly, pdb, outputs

def _declare_assembly_files(ctx, toolchain, is_executable, library_extension, library_infos):
    output_dir = ctx.attr.target_framework

    exe_extension = dotnetos_to_exe_extension(toolchain.default_dotnetos) if is_executable else library_extension
    assembly = ctx.actions.declare_file(paths.join(output_dir, ctx.attr.name + exe_extension))

    extensions = [
        ".deps.json",
        # already declared as `assembly` if not executable
        library_extension if is_executable else None,
    ]

    # todo toggle this when not debug
    pdb = ctx.actions.declare_file(paths.join(output_dir, ctx.attr.name + ".pdb"))

    if is_executable:
        extensions.extend([
            ".runtimeconfig.json",
            # todo find out when this is NOT output
            ".runtimeconfig.dev.json",
        ])

    file_paths = [
        paths.join(output_dir, ctx.attr.name + ext)
        for ext in extensions
        if ext != None
    ]

    for li in library_infos.to_list():
        file_paths.extend([
            paths.join(output_dir, f.basename)
            for f in (li.assembly, li.pdb)
        ])

    files = [
        ctx.actions.declare_file(file_path)
        for file_path in file_paths
    ]

    return assembly, pdb, files

def _make_compile_file(ctx, toolchain, is_executable, output_path, restore_file, library_infos):
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
                paths.join(STARTUP_DIR, li.assembly.path),
            ),
            {
                "Include": paths.split_extension(
                    li.assembly.basename,
                )[0],
            },
        )
        for li in library_infos.to_list()
    ]

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
