load("@bazel_skylib//lib:paths.bzl", "paths")
load("//dotnet/private/msbuild:xml.bzl", "make_project_file")
load(":common.bzl", "get_nuget_files")
load("//dotnet/private:context.bzl", "make_builder_cmd")
load("//dotnet/private:providers.bzl", "DotnetRestoreInfo", "NuGetPackageInfo")

def restore(ctx, dotnet):
    args, outputs = make_builder_cmd(ctx, dotnet, "restore")

    restore_dir = ctx.actions.declare_directory("restore")

    # we don't really need this, but this way, if the restore fails, bazel will fail the build
    # because this file wasn't created
    assets_json = ctx.actions.declare_file("restore/project.assets.json")
    outputs.extend([restore_dir, assets_json])

    dep_files = process_deps(dotnet, ctx.attr.deps)

    inputs = depset(
        direct = [ctx.file.project_file, dotnet.sdk.config.nuget_config],
        transitive = [dep_files],
    )

    ctx.actions.run(
        mnemonic = "NuGetRestore",
        inputs = inputs,
        outputs = outputs,
        executable = dotnet.sdk.dotnet,
        arguments = [args],
        env = dotnet.env,
        tools = dotnet.builder.files,
    )

    return DotnetRestoreInfo(
        project_file = ctx.file.project_file,
        dep_files = dep_files,
        restore_dir = restore_dir,
        target_framework = ctx.attr.target_framework,
    ), outputs

def process_deps(dotnet, deps):
    tfm = dotnet.config.tfm

    files = []
    transitive = []
    for dep in getattr(dotnet.config, "tfm_deps", []):
        get_nuget_files(dep, tfm, transitive)

    for dep in getattr(dotnet.config, "implicit_deps", []):
        get_nuget_files(dep, tfm, transitive)

    for dep in deps:
        if DotnetRestoreInfo in dep:
            info = dep[DotnetRestoreInfo]

            # MSBuild Restore is going to unconditionally traverse the entire project graph to
            # compute the full transitive closure of package files for *every* project file via a static
            # graph evaluation of project files. make sure the project_file is available as well as all
            # package files that those project files reference.
            files.append(info.project_file)
            transitive.append(info.dep_files)
        elif NuGetPackageInfo in dep:
            get_nuget_files(dep, tfm, transitive)
        else:
            fail("Unkown dependency type: {}".format(dep))

    return depset(files, transitive = transitive)
