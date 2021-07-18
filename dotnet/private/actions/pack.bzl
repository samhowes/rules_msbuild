load("@bazel_skylib//lib:paths.bzl", "paths")
load(":common.bzl", "declare_caches", "write_cache_manifest")
load("//dotnet/private:context.bzl", "dotnet_exec_context", "make_builder_cmd")
load("//dotnet/private:providers.bzl", "DotnetPublishInfo")
load("//dotnet:util.bzl", "to_manifest_path")

def pack(ctx):
    info = ctx.attr.target[DotnetPublishInfo]
    restore = info.restore

    dotnet = dotnet_exec_context(ctx, False, False, restore.target_framework)

    basename = paths.split_extension(ctx.file.project_file.basename)[0]
    nupkg = ctx.actions.declare_file(basename + "." + ctx.attr.version + ".nupkg")

    cache = declare_caches(ctx, "pack", info.library.project_cache)
    cache_manifest = write_cache_manifest(ctx, cache, info.caches)

    args, cmd_outputs = make_builder_cmd(ctx, dotnet, "pack")
    args.add_all(["--version", ctx.attr.version])

    inputs = depset([cache_manifest], transitive = [info.files])
    outputs = [nupkg, cache.result] + cmd_outputs

    #    runfiles = info.runfiles.to_list()
    #    if len(runfiles) > 0:
    #        runfiles_manifest = ctx.actions.declare_file(ctx.attr.name + ".runfiles_manifest")
    #        ctx.actions.write(
    #            runfiles_manifest,
    #            "\n".join([
    #                "%s %s" % (to_manifest_path(ctx, r), r.path)
    #                for r in runfiles
    #            ]),
    #        )
    #        direct_inputs.append(runfiles_manifest)
    #
    #    inputs = depset(direct_inputs, transitive = transitive_inputs)
    #    outputs = [nupkg] + cmd_outputs

    ctx.actions.run(
        mnemonic = "DotnetPack",
        inputs = inputs,
        outputs = outputs,
        executable = dotnet.sdk.dotnet,
        arguments = [args],
        env = dotnet.env,
        tools = dotnet.builder.files,
    )
    return nupkg
