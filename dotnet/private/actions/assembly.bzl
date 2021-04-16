load("@bazel_skylib//lib:paths.bzl", "paths")
load("@bazel_skylib//lib:dicts.bzl", "dicts")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo", "NuGetPackageInfo")
load("//dotnet/private/msbuild:xml.bzl", "INTERMEDIATE_BASE", "make_project_file")
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

    launch_data = {
        #        "dotnet_bin_path": sdk.dotnet.short_path.split("/", 1)[1],
        #        "target_bin_path": ctx.workspace_name + "/" + info.assembly.short_path,
        "dotnet_bin_path": sdk.dotnet.path,
        "target_bin_path": info.assembly.short_path,
        "dotnet_root": sdk.root_file.dirname,
        "dotnet_args": _format_launcher_args([]),
        "assembly_args": _format_launcher_args([]),
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
    if dotnet.os == "windows":
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

def _format_launcher_args(args):
    return " ".join(["\"{}\"".format(a) for a in args])

def emit_assembly(ctx, dotnet):
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
    sdk = dotnet.sdk

    output_dir = None
    if not dotnet.config.is_precise:
        output_dir = ctx.actions.declare_directory(output_path)

    ### declare and prepare all the build inputs/outputs
    deps = dotnet.config.implicit_deps + getattr(ctx.attr, "deps", [])  # dotnet_tool_binary can't have any deps
    dep_files = process_deps(dotnet, deps)
    project_file = make_project_file(ctx, intermediate_path, sdk.config.nuget_config, dotnet.config.is_executable, dep_files)

    restore_outputs = restore(ctx, dotnet, intermediate_path, project_file, dep_files)
    assembly, runtime, private = _declare_assembly_files(ctx, output_path, dotnet.config.is_executable)
    args, cmd_outputs = make_exec_cmd(ctx, dotnet, "build", project_file, intermediate_path)

    ### collect build inputs/outputs
    inputs = depset(
        direct = ctx.files.srcs + [project_file] + restore_outputs,
        transitive = [dep_files.inputs, sdk.init_files],
    )

    copied_dep_files = [
        ctx.actions.declare_file(f.basename, sibling = assembly)
        for f in dep_files.copied_files.to_list()
    ]

    outputs = runtime + private + copied_dep_files + cmd_outputs
    if output_dir != None:
        outputs.append(output_dir)

    ctx.actions.run(
        mnemonic = "DotnetBuild",
        inputs = inputs,
        outputs = outputs,
        executable = dotnet.sdk.dotnet,
        arguments = [args],
        env = dotnet.env,
        tools = dotnet.tools,
    )

    info = DotnetLibraryInfo(
        assembly = assembly,
        output_dir = output_dir,
        project_file = project_file,
        runtime = depset(runtime + copied_dep_files),
        package_runtimes = dep_files.package_runtimes,
        build = depset(runtime + [project_file], transitive = [dep_files.inputs]),
    )
    return info, outputs + restore_outputs, private

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
        references, packages, copied_files
    """

    copy_packages = dotnet.config.is_executable
    tfm = dotnet.config.tfm

    references = []
    packages = []
    package_runtimes = []
    copied_files = []
    inputs = []

    for dep in deps:
        if DotnetLibraryInfo in dep:
            info = dep[DotnetLibraryInfo]
            references.append(info.project_file)
            copied_files.append(info.runtime)
            if copy_packages:
                copied_files.append(info.package_runtimes)
            inputs.append(info.build)

        elif NuGetPackageInfo in dep:
            pkg = dep[NuGetPackageInfo]
            framework_info = getattr(pkg.frameworks, tfm, None)
            if framework_info == None:
                fail("TargetFramework {} was not fetched for pkg dep {}. Fetched tfms: {}.".format(
                    tfm,
                    pkg.name + ":" + pkg.version,
                    ", ".join([k for k, v in pkg.frameworks]),
                ))
            packages.append(pkg)

            # todo(#67) restrict these inputs
            inputs.append(pkg.all_files)

            inputs.append(framework_info.build)
            package_runtimes.append(framework_info.runtime)
            if copy_packages:
                copied_files.append(framework_info.runtime)
        else:
            fail("Unkown dependency type: {}".format(dep))

    inputs.extend(copied_files)
    return struct(
        references = references,
        packages = packages,
        package_runtimes = depset(transitive = package_runtimes),
        copied_files = depset(transitive = copied_files),
        inputs = depset(transitive = inputs),
    )
