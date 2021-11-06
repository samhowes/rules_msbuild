load("@bazel_skylib//lib:paths.bzl", "paths")
load("@rules_dotnet_runtime//dotnet:defs.bzl", "DotnetPublishInfo")
load(":common.bzl", "cache_set", "declare_caches", "write_cache_manifest")
load("//dotnet/private:context.bzl", "dotnet_exec_context", "make_builder_cmd")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo", "MSBuildDirectoryInfo", _DotnetPublishInfo = "DotnetPublishInfo")
load("//dotnet/private/util:util.bzl", "to_manifest_path")

def publish(ctx):
    info = ctx.attr.target[DotnetLibraryInfo]
    restore = info.restore

    dotnet = dotnet_exec_context(ctx, info.executable, False, restore.target_framework)

    output_dir = ctx.actions.declare_directory(paths.join("publish", dotnet.config.tfm))

    launcher = None
    launcher_windows = None
    if info.executable:
        launcher = ctx.actions.declare_file(paths.join("publish", dotnet.config.tfm, restore.assembly_name))
        launcher_windows = ctx.actions.declare_file(restore.assembly_name + ".exe", sibling = launcher)

    cache = declare_caches(ctx, "publish")

    caches = info.caches
    cache_manifest = write_cache_manifest(ctx, cache, caches)

    args, cmd_outputs = make_builder_cmd(ctx, dotnet, "publish", restore.directory_info, restore.assembly_name)

    args.add_all(["--launcher_template", ctx.file._launcher_template])

    runfiles_manifest = manual_runfiles(ctx, info)

    inputs = depset(
        [cache_manifest, ctx.file._launcher_template, runfiles_manifest],
        transitive = [info.files, info.runfiles],
    )
    outputs = [output_dir, cache.result, cache.project] + cmd_outputs + (
        [launcher, launcher_windows] if info.executable else []
    )

    ctx.actions.run(
        mnemonic = "DotnetPublish",
        inputs = inputs,
        outputs = outputs,
        executable = dotnet.sdk.dotnet,
        arguments = [args],
        env = dotnet.env,
        tools = dotnet.builder.files,
    )

    publish_info = _DotnetPublishInfo(
        output_dir = output_dir,
        files = depset(direct = outputs, transitive = [inputs]),
        caches = cache_set([cache], transitive = [caches]),
        library = info,
        restore = restore,
        runfiles_manifest = runfiles_manifest,
        public = DotnetPublishInfo(
            launcher = launcher,
            launcher_windows = launcher_windows,
            files = depset(outputs),
            output_directory = output_dir,
        ),
    )

    return publish_info

def manual_runfiles(ctx, info):
    runfiles = info.runfiles.to_list()
    runfiles_manifest = ctx.actions.declare_file(ctx.attr.name + ".runfiles_manifest")
    ctx.actions.write(
        runfiles_manifest,
        "\n".join([
            "%s %s" % (to_manifest_path(ctx, r), to_manifest_path(ctx, r, True))
            for r in runfiles
        ]),
    )
    return runfiles_manifest
