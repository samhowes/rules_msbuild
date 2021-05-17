load("@bazel_skylib//lib:paths.bzl", "paths")
load("@bazel_skylib//lib:dicts.bzl", "dicts")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo", "NuGetPackageInfo")
load("//dotnet/private/msbuild:xml.bzl", "INTERMEDIATE_BASE", "STARTUP_DIR", "make_project_file")
load("//dotnet/private/actions:restore.bzl", "restore")
load(
    "//dotnet/private:context.bzl",
    "make_exec_cmd",
)

def make_launcher(ctx, dotnet, info):
    sdk = dotnet.sdk

    launcher = ctx.actions.declare_file(
        ctx.attr.name + dotnet.ext,
        sibling = info.output_dir,
    )

    bin_launcher = dotnet.os == "windows"

    # ../dotnet_sdk/dotnet => dotnet_sdk/dotnet
    dotnet_path = sdk.dotnet.short_path.split("/", 1)[1]

    launch_data = {
        "dotnet_bin_path": dotnet_path,
        "target_bin_path": paths.join(ctx.workspace_name, info.assembly.short_path),
        "output_dir": info.output_dir.short_path,
        "dotnet_root": sdk.root_file.dirname,
        "dotnet_args": _format_launcher_args([], bin_launcher),
        "assembly_args": _format_launcher_args([], bin_launcher),
        "workspace_name": ctx.workspace_name,
        "dotnet_cmd": "exec",
        "dotnet_logger": "junit",
        "log_path_arg_name": "LogFilePath",
    }

    if getattr(dotnet.config, "is_test", False):
        launch_data = dicts.add(launch_data, {
            "dotnet_cmd": "test",
        })

    launcher_template = ctx.file._launcher_template
    if bin_launcher:
        args = ctx.actions.args()
        args.add(dotnet.builder)
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
                dotnet.builder,
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

def _format_launcher_args(args, bin_launcher):
    if not bin_launcher:
        return " ".join(["\"{}\"".format(a) for a in args])
    else:
        return "*~*".join(args)

def emit_tool_binary(ctx, dotnet):
    """Create a binary used for the dotnet toolchain itself.

    This implementation assumes that no other targets will depend on this binary for anything other than executing as a
    tool. Since this is part of the toolchain itself, it can't execute a multiphase restore, build, publish,
    because bazel's sandboxing/remote execution will cause msbuild to not be able to find reference paths between
    actions. So instead, we execute all msbuild steps in a single action via invoking the publish target directly.
    """
    output_dir = ctx.actions.declare_directory(paths.join(dotnet.config.output_dir_name, "publish"))
    dep_files = process_deps(dotnet, ctx.attr.deps)
    project_file = make_project_file(ctx, dotnet, dep_files, STARTUP_DIR)
    assembly = ctx.actions.declare_file(paths.join(output_dir.short_path, ctx.attr.name + ".dll"))
    files = struct(
        output_dir = output_dir,
    )

    args, cmd_outputs, _, _ = make_exec_cmd(ctx, dotnet, "publish", project_file, files)

    args.add("-restore")

    direct_inputs = ctx.files.srcs + [project_file, dotnet.sdk.config.nuget_config]
    source_project_file = getattr(ctx.file, "project_file", None)
    if source_project_file != None:
        direct_inputs.append(source_project_file)
    inputs = depset(
        direct = direct_inputs,
        transitive = [dep_files.build, dotnet.sdk.init_files, dotnet.sdk.packs],
    )
    outputs = [output_dir, assembly] + cmd_outputs

    ctx.actions.run(
        mnemonic = "DotnetBuild",
        inputs = inputs,
        outputs = outputs,
        executable = dotnet.sdk.dotnet,
        arguments = [args],
        env = dotnet.env,
    )
    return DotnetLibraryInfo(
        assembly = assembly,
        output_dir = output_dir,
        project_file = project_file,
        runtime = depset(outputs),
        build = depset(),
        restore = depset(),
        target_framework = ctx.attr.target_framework,
    ), outputs + [project_file]

def emit_assembly(ctx, dotnet):
    """Compile a dotnet assembly with the provided project template

    Args:
        ctx: a ctx with //dotnet/private/rules:common.bzl.DOTNET_ATTRS
        is_executable: if the assembly should be executable
    Returns:
        Tuple: the emitted assembly and all outputs
    """
    sdk = dotnet.sdk

    output_dir = None
    if not dotnet.config.is_precise:
        output_dir = ctx.actions.declare_directory(dotnet.config.output_dir_name)

    ### declare and prepare all the build inputs/outputs
    dep_files = process_deps(dotnet, ctx.attr.deps)
    project_file = make_project_file(ctx, dotnet, dep_files)

    restore_outputs = restore(ctx, dotnet, project_file, dep_files)
    assembly, runtime, private = _declare_assembly_files(ctx, dotnet.config.output_dir_name, dotnet.config.is_executable)
    files = struct(
        output_dir = output_dir,
        # todo(#6) make this a full depset including dependencies
        content = depset(getattr(ctx.files, "content", [])),
        data = depset(getattr(ctx.files, "data", [])),
    )
    args, cmd_outputs, cmd_inputs, build_cache = make_exec_cmd(ctx, dotnet, "build", project_file, files)

    content = getattr(ctx.files, "content", [])

    ### collect build inputs/outputs
    inputs = depset(
        direct = ctx.files.srcs + content + [project_file] + restore_outputs + cmd_inputs,
        transitive = [dep_files.build, sdk.init_files],
    )

    intermediate_dir = ctx.actions.declare_directory(paths.join(dotnet.config.intermediate_path, dotnet.config.tfm))

    outputs = runtime + private + cmd_outputs + [intermediate_dir]

    for f in [output_dir]:
        if f != None:
            outputs.append(f)

    ctx.actions.run(
        mnemonic = "DotnetBuild",
        inputs = inputs,
        outputs = outputs,
        executable = dotnet.sdk.dotnet,
        arguments = [args],
        env = dotnet.env,
        tools = dotnet.tools,
    )

    runtime_files = runtime
    if getattr(dotnet.config, "is_executable", False):
        runtime_files += private

    info = DotnetLibraryInfo(
        assembly = assembly,
        intermediate_dir = intermediate_dir,
        output_dir = output_dir,
        project_file = project_file,
        build_cache = build_cache,
        runtime = depset(runtime),
        build = depset(
            runtime + [project_file, build_cache],
            transitive = [dep_files.build],
        ),
        restore = depset(
            [project_file],
            transitive = [dep_files.restore],
        ),
        target_framework = ctx.attr.target_framework,
        data = files.data,
        content = files.content,
    )
    return info, outputs + restore_outputs + [project_file], private

def _declare_assembly_files(ctx, output_dir, is_executable):
    name = ctx.attr.name
    assembly = ctx.actions.declare_file(paths.join(output_dir, name + ".dll"))
    runtime = [assembly]

    private = [ctx.actions.declare_file(name + ".deps.json", sibling = assembly)]

    if True:
        # todo(#21) toggle this when not debug
        runtime.append(ctx.actions.declare_file(name + ".pdb", sibling = assembly))

    if is_executable:
        private.extend([
            ctx.actions.declare_file(name + ext, sibling = assembly)
            for ext in [
                ".runtimeconfig.json",
                # todo(#22) find out when this is NOT output
                ".runtimeconfig.dev.json",
            ]
        ])

    return assembly, runtime, private

def process_deps(dotnet, deps):
    """Split deps into assembly references and packages

    Args:
        deps: the deps of a dotnet assembly ctx
        tfm: the target framework moniker being built
        copy_packages: Whether to copy package files to the output directory. NuGet packages are only copied for
            executables.
    Returns:
        references, packages
    """

    tfm = dotnet.config.tfm

    references = []
    packages = []

    build = []
    restore_list = []
    common = []
    for dep in getattr(dotnet.config, "tfm_deps", []):
        _get_nuget_files(dep, tfm, [], common)

    for dep in getattr(dotnet.config, "implicit_deps", []):
        _get_nuget_files(dep, tfm, packages, common)

    for dep in deps:
        if DotnetLibraryInfo in dep:
            info = dep[DotnetLibraryInfo]
            references.append(info.project_file)
            build.append(info.build)
            restore_list.append(info.restore)

        elif NuGetPackageInfo in dep:
            _get_nuget_files(dep, tfm, packages, common)
        else:
            fail("Unkown dependency type: {}".format(dep))

    build.extend(common)
    restore_list.extend(common)
    return struct(
        references = references,
        packages = packages,
        build = depset(transitive = build),
        restore = depset(transitive = restore_list),
    )

def _get_nuget_files(dep, tfm, packages, inputs):
    pkg = dep[NuGetPackageInfo]
    framework_info = pkg.frameworks.get(tfm, None)
    if framework_info == None:
        fail("TargetFramework {} was not fetched for pkg dep {}. Fetched tfms: {}.".format(
            tfm,
            pkg.name,
            ", ".join([k for k, v in pkg.frameworks.items()]),
        ))
    packages.append(struct(name = pkg.name, version = framework_info.version))

    # todo(#67) restrict these inputs
    inputs.append(framework_info.all_dep_files)
