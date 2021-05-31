"""This module creates a `DotnetContext` struct. Instances of this object are referred to as `dotnet`.

A DotnetContext is the "context" in which we are interacting with the dotnet binary. This is necessary because there are
a couple different contexts in which we are interacting with the dotnet binary, but in these different contexts, we
still want to perform the same fundamental actions with the dotnet binary.

1) The "primary" context that we'll be interacting with dotnet in, is when we are performing the steps to actually build
    an assembly for a user that wrote a `dotnet_binary` rule in a BUILD file. This context will not be invoking
    `dotnet build` directly, but instead passing build instructions to //dotnet/tools/builder.

    The "builder" takes care of a couple administrative things to de-bazilfy the inputs to dotnet, and to bazelify
    dotnet's output, before invoking dotnet build directly.

2) "Building the builder" context. Before we can enter the "primary" context we have to build the builder that assists
    with the primary context. This context invokes dotnet directly to compile the builder sources into the builder dll.
    This context cannot depend on the toolchain, because the toolchain depends on this context.

3) "NuGet Fetch" context. NuGet fetch happens in the loading phase, not the execution phase, as such, we don't have
    access to the toolchain and resolved labels from the build file, because those are constructed for the execution
    phase. In this context, we execute dotnet with repository_ctx.execute(), which takes in a list of strings as
    arguments, the other contexts need an "args" object produced by ctx.actions.args()
"""

load("//dotnet/private:providers.bzl", "DotnetSdkInfo")
load("//dotnet/private/msbuild:environment.bzl", "NUGET_ENVIRONMENTS", "isolated_environment")
load("//dotnet/private/msbuild:xml.bzl", "EXEC_ROOT", "INTERMEDIATE_BASE")
load("@bazel_skylib//lib:paths.bzl", "paths")
load("@bazel_skylib//lib:dicts.bzl", "dicts")

def dotnet_exec_context(ctx, is_executable, is_test = False, target_framework = None):
    toolchain = None
    sdk = None

    # the builder doesn't have a toolchain: it is part of the toolchain
    sdk_attr = getattr(ctx.attr, "dotnet_sdk", None)
    if sdk_attr != None:
        sdk = sdk_attr[DotnetSdkInfo]
    else:
        toolchain = ctx.toolchains["@my_rules_dotnet//dotnet:toolchain"]
        sdk = toolchain.sdk

    implicit_deps = []
    if is_test:
        # for out-of-the-box bazel-compatible test logging
        implicit_deps.append(sdk.config.test_logger)

    tfm = getattr(ctx.attr, "target_framework", target_framework)
    tfm_info = sdk.config.tfm_mapping.get(tfm, None)
    if tfm_info == None:
        fail("Tfm {} was not configured for restore by nuget. If this was not a mistake, please add it to your " +
             "nuget_fetch rule.".format(tfm))

    dotnet = dotnet_context(
        sdk.root_file.dirname,
        sdk.dotnetos,
        None if toolchain == None else toolchain._builder,
        sdk,
        tfm = tfm,
        output_dir_name = tfm,
        is_executable = is_executable,
        # todo(73) remove this
        is_precise = True if toolchain == None else False,
        implicit_deps = implicit_deps,
        tfm_deps = tfm_info.implicit_deps,
        is_test = is_test,
        intermediate_path = INTERMEDIATE_BASE,
    )

    dotnet.env["BINDIR"] = ctx.bin_dir.path
    return dotnet

def dotnet_context(sdk_root, os, builder = None, sdk = None, **kwargs):
    ext = ".exe" if os == "windows" else ""
    return struct(
        sdk_root = sdk_root,
        os = os,
        path = paths.join(sdk_root, "dotnet" + ext),
        env = _make_env(sdk_root, os),
        builder = builder,
        sdk = sdk,
        ext = ext,
        config = struct(
            **kwargs
        ),
    )

def _make_env(dotnet_sdk_root, os):
    env = {
        "DOTNET_CLI_HOME": dotnet_sdk_root,
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        # isolate Dotnet from using the system installed sdk
        "DOTNET_MULTILEVEL_LOOKUP": "0",
        "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
        "DOTNET_NOLOGO": "1",
        "NUGET_SHOW_STACK": "true",
        # "BUILDER_DEBUG": "1",
    }

    if os not in NUGET_ENVIRONMENTS:
        fail("No nuget environment configuration for os {}".format(os))

    env_dict = NUGET_ENVIRONMENTS[os]
    nuget_environment_info = isolated_environment(dotnet_sdk_root)

    for name, env_name in env_dict.items():
        if env_name == "":
            continue
        env[env_name] = getattr(nuget_environment_info, name)

    return env

def make_builder_cmd(ctx, dotnet, action):
    outputs = []
    binlog = None
    if True:
        # todo(#51) disable when not debugging the build
        binlog = ctx.actions.declare_file(ctx.attr.name + ".binlog")
        outputs.append(binlog)

    args = ctx.actions.args()
    args.add_all([
        dotnet.builder.assembly.path,
        action,
        "--sdk_root",
        dotnet.sdk.sdk_root.path,
        "--project_file",
        ctx.file.project_file,
        "--bazel_bin_dir",
        ctx.bin_dir.path,
        "--tfm",
        dotnet.config.tfm,
        "--bazel_output_base",
        dotnet.sdk.config.trim_path,
        "--workspace",
        ctx.workspace_name,
        "--package",
        ctx.label.package,
        "--label_name",
        ctx.label.name,
        "--nuget_config",
        dotnet.sdk.config.nuget_config,
        "--directory_bazel_props",
        dotnet.sdk.bazel_props,
    ])
    if dotnet.config.is_test:
        args.add_all(["--is_test", True])
    return args, outputs

def make_exec_cmd(ctx, dotnet, msbuild_target, proj, files):
    """Create a command for use during the execution phase"""
    outputs = []  # todo(#51) disable when not debugging the build
    binlog_path = None
    if True:
        binlog_path = proj.path + ".{}.binlog".format(msbuild_target)
        outputs.append(ctx.actions.declare_file(paths.basename(binlog_path)))

    args = ctx.actions.args()
    inputs = []
    cache_file = None
    if dotnet.builder == None:
        arg_list = make_cmd(
            proj.path,
            msbuild_target,
            binlog_path,
        )
        for arg in arg_list:
            args.add(arg)
    else:
        builder_args = []
        if msbuild_target == "build":
            cache_file = ctx.actions.declare_file(proj.basename + "." + msbuild_target + ".cache")
            outputs.append(cache_file)

        if msbuild_target == "build" or msbuild_target == "publish":
            builder_args.extend([
                ["--content", ";".join([f.path for f in files.content.to_list()])],
            ])

        if msbuild_target == "publish":
            runfiles_directory = paths.replace_extension(paths.basename(proj.path), ".dll") + ".runfiles"
            builder_args.extend([
                ["--runfiles", ";".join([f.path for f in files.data.to_list()])],
                ["--runfiles_directory", runfiles_directory],
            ])

        params_file = ctx.actions.declare_file(paths.basename(proj.path) + "." + msbuild_target + ".params")
        ctx.actions.write(
            params_file,
            "\n".join([
                " ".join(a)
                for a in builder_args
            ]),
        )
        inputs.append(params_file)

        args.add("@file", params_file.path)

    return args, outputs, inputs, cache_file

def make_cmd(project_path, msbuild_target, binlog_path = None):
    args_list = [
        "msbuild",
        "/t:" + msbuild_target,
        "-nologo",
        "-verbosity:quiet",
    ]

    args_list.append(project_path)

    if binlog_path:
        args_list.append("-bl:{}".format(binlog_path))

    return args_list
