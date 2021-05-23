load("//dotnet/private:providers.bzl", "DotnetLibraryInfo", "DotnetRestoreInfo")
load("//dotnet/private:context.bzl", "make_builder_cmd")
load("@bazel_skylib//lib:paths.bzl", "paths")

def build_assembly(ctx, dotnet):
    restore_info = ctx.attr.restore[DotnetRestoreInfo]
    output_dir = ctx.actions.declare_directory(dotnet.config.output_dir_name)
    assembly = ctx.actions.declare_file(paths.join(output_dir.basename, ctx.attr.name + ".dll"))
    build_cache = ctx.actions.declare_file(restore_info.generated_project_file.basename + ".build.cache")

    dep_files = _process_deps(ctx, restore_info)
    args, cmd_outputs = make_builder_cmd(ctx, dotnet, "build", restore_info.generated_project_file)

    # todo(#6) make this a full depset including dependencies
    content = depset(getattr(ctx.files, "content", []))
    data = depset(getattr(ctx.files, "data", []))

    # This could be a lot of files. We would use args.use_params_file, but that would put *all*
    # the args into a params file, and dotnet does not support that.
    # By convention, the builder will look for proj.srcs for the sources to compile
    srcs = ctx.actions.declare_file(
        restore_info.generated_project_file.basename + ".srcs",
        sibling = restore_info.generated_project_file,
    )
    ctx.actions.write(srcs, "\n".join([s.path for s in ctx.files.srcs]))

    inputs = depset(
        [srcs, restore_info.intermediate_dir] +
        ctx.files.srcs,
        transitive = [dep_files, content, dotnet.sdk.init_files],
    )

    outputs = [assembly, output_dir, build_cache] + cmd_outputs

    ctx.actions.run(
        mnemonic = "MSBuild",
        inputs = inputs,
        outputs = outputs,
        executable = dotnet.sdk.dotnet,
        arguments = [args],
        env = dotnet.env,
        tools = [dotnet.builder],
    )

    info = DotnetLibraryInfo(
        assembly = assembly,
        output_dir = output_dir,
        build_cache = build_cache,
        content = content,
        data = data,
    )

    return info, outputs

def _process_deps(ctx, restore_info):
    files = [restore_info.generated_project_file, restore_info.source_project_file]
    for d in ctx.attr.deps:
        if DotnetLibraryInfo in d:
            info = d[DotnetLibraryInfo]
            files.extend([
                info.output_dir,
                info.build_cache,
            ])

    return depset(
        files,
        # we need the full transitive closure of dependency files here because MSBuild
        # could decide to copy some of these files to the output directory
        # this will definitely happen for an exe build, and may happen for other assemblies
        # depending on the user's project settings
        transitive = [restore_info.dep_files],
    )
