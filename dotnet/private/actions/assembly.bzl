load("@bazel_skylib//lib:paths.bzl", "paths")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo")
load("//dotnet/private/msbuild:xml.bzl", "INTERMEDIATE_BASE", "make_compile_file")
load("//dotnet/private/actions:restore.bzl", "restore")
load(
    "//dotnet/private:context.bzl",
    "make_exec_cmd",
)

def make_launcher(ctx, dotnet, toolchain, assembly):
    sdk = toolchain.sdk

    ext = ".exe" if toolchain.default_dotnetos == "windows" else ""
    launcher = ctx.actions.declare_file(
        ctx.attr.name + ext,
        sibling = assembly,
    )

    launch_data = {
        "dotnet_root": sdk.root_file.dirname,
        "dotnet_bin_path": sdk.dotnet.short_path.split("/", 1)[1],  # ctx.workspace_name + "/" + sdk.dotnet.path,
        "target_bin_path": ctx.workspace_name + "/" + assembly.short_path,
        "workspace_name": ctx.workspace_name,
    }

    launcher_template = ctx.file._launcher_template
    if toolchain.default_dotnetos == "windows":
        args = ctx.actions.args()
        args.add(toolchain._builder)
        args.add("launcher")
        args.add(launcher_template)
        args.add(launcher)

        args.add("symlink_runfiles_enabled")
        args.add("0")

        args.add("dotnet_env")
        args.add(";".join([
            "{}={}".format(k, v)
            for k, v in dotnet.env.items()
        ]))

        for k, v in launch_data.items():
            args.add(k)
            args.add(v)

        ctx.actions.run(
            inputs = [launcher_template],
            outputs = [launcher],
            executable = sdk.dotnet,
            arguments = [args],
            env = dotnet.env,
            tools = [
                toolchain._builder,
            ],
        )
    else:
        substitutions = dict([
            ("%{}%".format(k), v)
            for k, v in launch_data.items()
        ])
        substitutions["%dotnet_env%"] = "\n".join([
            "export {}=\"{}\"".format(k, v)
            for k, v in dotnet.env.items()
        ])

        ctx.actions.expand_template(
            template = launcher_template,
            output = launcher,
            is_executable = True,
            substitutions = substitutions,
        )
    return launcher

def emit_assembly(ctx, dotnet, sdk, is_executable):
    """Compile a dotnet assembly with the provided project template

    Args:
        ctx: a ctx with //dotnet/private/rules:common.bzl.DOTNET_ATTRS
        is_executable: if the assembly should be executable
    Returns:
        Tuple: the emitted assembly and all outputs
    """
    compiliation_mode = ctx.var["COMPILATION_MODE"]
    output_path = ctx.attr.target_framework
    intermediate_path = INTERMEDIATE_BASE

    tfm = ctx.attr.target_framework
    all_outputs = []
    msbuild_outputs = []

    deps = getattr(ctx.attr, "deps", [])  # dotnet_tool_binary can't have any deps
    references, packages, copied_files = process_deps(deps, tfm)
    assembly, pdb, assembly_files, intermediate_files = _declare_assembly_files(ctx, output_path, intermediate_path)
    msbuild_outputs += assembly_files
    msbuild_outputs += intermediate_files
    all_outputs += assembly_files
    all_outputs += intermediate_files

    for cf in copied_files:
        f = ctx.actions.declare_file(cf.basename, sibling = assembly)
        msbuild_outputs.append(f)
        all_outputs.append(f)

    restore_file, restore_outputs, cmd_outputs = restore(ctx, dotnet, sdk, intermediate_path, packages)
    all_outputs += cmd_outputs

    compile_file = make_compile_file(ctx, is_executable, restore_file, references)

    executable_files = []
    if is_executable:
        executable_files = _make_executable_files(ctx, assembly, sdk)
        msbuild_outputs += executable_files
        all_outputs += executable_files

    args, cmd_outputs = make_exec_cmd(dotnet, ctx, "build", compile_file, intermediate_path)
    msbuild_outputs += cmd_outputs
    all_outputs += cmd_outputs

    inputs = depset(
        direct = (
            [compile_file, restore_file] +
            restore_outputs +
            ctx.files.srcs +
            copied_files +
            sdk.init_files
        ),
        transitive = [p.all_files for p in packages],
    )

    ctx.actions.run(
        mnemonic = "DotnetBuild",
        inputs = inputs,
        outputs = msbuild_outputs,
        executable = sdk.dotnet,
        arguments = [args],
        env = dotnet.env,
        tools = dotnet.tools,
    )

    return assembly, pdb, all_outputs

def _make_executable_files(ctx, assembly, sdk):
    name = ctx.attr.name

    files = [
        ctx.actions.declare_file(name + ext, sibling = assembly)
        for ext in [
            ".runtimeconfig.json",
            # todo(#22) find out when this is NOT output
            ".runtimeconfig.dev.json",
        ]
    ]

    return files

def _declare_assembly_files(ctx, output_dir, intermediate_path):
    assembly = ctx.actions.declare_file(paths.join(output_dir, ctx.attr.name + ".dll"))

    # todo(#21) toggle this when not debug
    pdb = ctx.actions.declare_file(ctx.attr.name + ".pdb", sibling = assembly)
    deps = ctx.actions.declare_file(ctx.attr.name + ".deps.json", sibling = assembly)

    intermediate_files = [
        ctx.actions.declare_file(
            paths.join(intermediate_path, ctx.attr.target_framework, ctx.attr.name + "." + ext),
        )
        for ext in [
            "AssemblyInfo.cs",
            "AssemblyInfoInputs.cache",
            "assets.cache",
            "csproj.FileListAbsolute.txt",
            "csprojAssemblyReference.cache",
            "dll",
            "pdb",
        ]
    ]
    return assembly, pdb, [assembly, pdb, deps], intermediate_files

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
            _collect_files(tinfo, copied_files, tfm, references, None)

    return references, packages, copied_files

def _collect_files(info, copied_files, tfm, references, packages = None):
    pkg = getattr(info, "package_info", None)
    if pkg != None:
        framework_info = getattr(pkg.frameworks, tfm, None)
        if framework_info == None:
            fail("TargetFramework {} was not fetched for pkg dep {}. Fetched tfms: {}. " +
                 "Make sure it is listed in `nuget_fetch` for your workspace.".format(
                     tfm,
                     pkg.name + ":" + pkg.version,
                     ", ".join([k for k, v in pkg.frameworks]),
                 ))

        copied_files.extend(framework_info.runtime.to_list())
        if packages != None:
            packages.append(pkg)
    else:
        copied_files.append(info.assembly)
        if info.pdb != None:
            copied_files.append(info.pdb)

        references.append(info.assembly)
