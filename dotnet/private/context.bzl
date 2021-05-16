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
load("//dotnet/private/msbuild:xml.bzl", "INTERMEDIATE_BASE", "STARTUP_DIR")
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
    return dotnet_context(
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
        tfm_deps = sdk.config.tfm_mapping[tfm].implicit_deps,
        is_test = is_test,
        intermediate_path = INTERMEDIATE_BASE,
    )

def dotnet_context(sdk_root, os, builder = None, sdk = None, **kwargs):
    ext = ".exe" if os == "windows" else ""
    return struct(
        sdk_root = sdk_root,
        os = os,
        path = paths.join(sdk_root, "dotnet" + ext),
        env = _make_env(sdk_root, os),
        builder = builder,
        sdk = sdk,
        tools = [builder] if builder != None else [],
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

def make_exec_cmd(ctx, dotnet, msbuild_target, proj, files, actual_target = None):
    """Create a command for use during the execution phase"""
    binlog = False  # todo(#51) disable when not debugging the build
    if True:
        binlog = True

    target_heuristics = True
    if actual_target != None:
        target_heuristics = False
    else:
        actual_target = msbuild_target

    if target_heuristics and msbuild_target == "build":
        # https://github.com/dotnet/msbuild/issues/5204
        actual_target = "GetTargetFrameworks;Build;GetCopyToOutputDirectoryItems;GetNativeManifest"

    arg_list, binlog_path = make_cmd(
        dotnet,
        proj.path,
        msbuild_target,
        binlog,
        actual_target,
    )

    msbuild_properties = {
        # we'll take care of making sure references are built, don't traverse them unnecessarily
        "BuildProjectReferences": "false",
        "RestoreRecursive": "false",
    }

    if target_heuristics and msbuild_target == "publish":
        msbuild_properties = dicts.add(msbuild_properties, {
            "PublishDir": paths.join(STARTUP_DIR, files.output_dir.path),
            "NoBuild": "true",
            "TreatWarningsAsErrors": "true",
        })

    for k, v in msbuild_properties.items():
        arg_list.append("/p:{}={}".format(k, v))

    outputs = []
    if binlog_path != None:
        outputs.append(ctx.actions.declare_file(paths.basename(binlog_path)))

    args = ctx.actions.args()
    inputs = []
    cache_file = None
    if target_heuristics and dotnet.builder != None:
        intermediate_path_full = paths.join(str(proj.dirname), dotnet.config.intermediate_path)
        args.add(dotnet.builder.path)
        args.add(msbuild_target)
        cache_file = ctx.actions.declare_file(proj.basename + "." + msbuild_target + ".cache")
        outputs.append(cache_file)

        # these args specify lists of files, which could get very long. We can't take advantage of
        # params files because the dotnet cli needs to execute the builder, and the dotnet cli doesn't have
        # support for params files. Instead, we'll just write our own params file manually
        builder_args = [
            ["--package", ctx.label.package],
            ["--sdk_root", dotnet.sdk.sdk_root.path],
            ["--intermediate_base", intermediate_path_full],
            ["--tfm", dotnet.config.tfm],
            ["--bazel_output_base", dotnet.sdk.config.trim_path],
            ["--project_file", proj.path],
            ["--workspace", ctx.workspace_name],
        ]

        if msbuild_target == "build" or msbuild_target == "publish":
            builder_args.extend([
                ["--content", ";".join([f.path for f in files.content.to_list()])],
                ["--output_directory", files.output_dir.path],
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
        args.add("--")
        args.add(dotnet.path)

    for arg in arg_list:
        args.add(arg)

    return args, outputs, inputs, cache_file

def make_cmd(dotnet, project_path, msbuild_target, binlog = False, actual_target = None):
    if actual_target == None:
        actual_target = msbuild_target

    args_list = [
        "msbuild",
        "/t:" + actual_target,
        "-nologo",
    ]

    args_list.append(project_path)

    binlog_path = None
    if binlog:
        binlog_path = project_path + ".{}.binlog".format(msbuild_target.rstrip(";"))
        args_list.append("-bl:{}".format(binlog_path))

    return args_list, binlog_path
