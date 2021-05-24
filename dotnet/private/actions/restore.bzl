load("@bazel_skylib//lib:paths.bzl", "paths")
load("//dotnet/private/msbuild:xml.bzl", "make_project_file")
load("//dotnet/private:context.bzl", "make_builder_cmd")
load("//dotnet/private:providers.bzl", "DotnetRestoreInfo", "NuGetPackageInfo")

def restore(ctx, dotnet):
    source_project_file = ctx.file.project_file
    generated_project_file = ctx.actions.declare_file(source_project_file.basename)
    args, outputs = make_builder_cmd(ctx, dotnet, "restore", generated_project_file)

    outputs.append(generated_project_file)
    restore_dir = ctx.actions.declare_directory("restore")

    # we don't really need this, but this way, if the restore fails, bazel will fail the build
    # because this file wasn't created
    assets_json = ctx.actions.declare_file("restore/project.assets.json")
    outputs.extend([restore_dir, assets_json])

    dep_files = process_deps(dotnet, ctx.attr.deps)

    inputs = depset(
        direct = [source_project_file, dotnet.sdk.config.nuget_config],
        transitive = [dep_files, dotnet.sdk.init_files, dotnet.sdk.packs],
    )

    ctx.actions.run(
        mnemonic = "NuGetRestore",
        inputs = inputs,
        outputs = outputs,
        executable = dotnet.sdk.dotnet,
        arguments = [args],
        env = dotnet.env,
        tools = [dotnet.builder],
    )

    return DotnetRestoreInfo(
        source_project_file = source_project_file,
        generated_project_file = generated_project_file,
        dep_files = dep_files,
        restore_dir = restore_dir,
        target_framework = ctx.attr.target_framework,
    ), outputs

def process_deps(dotnet, deps):
    tfm = dotnet.config.tfm

    files = []
    package_files = []
    for dep in getattr(dotnet.config, "tfm_deps", []):
        _get_nuget_files(dep, tfm, package_files)

    for dep in getattr(dotnet.config, "implicit_deps", []):
        _get_nuget_files(dep, tfm, package_files)

    for dep in deps:
        if DotnetRestoreInfo in dep:
            info = dep[DotnetRestoreInfo]

            # MSBuild Restore is going to unconditionally traverse the entire project graph to
            # compute the full transitive closure of package files for *every* project file.
            # make sure the source_project_file (user-owned) is available as well as all package
            # files that those project files reference.
            files.append(info.source_project_file)
            package_files.append(info.dep_files)
        elif NuGetPackageInfo in dep:
            _get_nuget_files(dep, tfm, package_files)
        else:
            fail("Unkown dependency type: {}".format(dep))

    return depset(files, transitive = package_files)

def _get_nuget_files(dep, tfm, files):
    pkg = dep[NuGetPackageInfo]
    framework_info = pkg.frameworks.get(tfm, None)
    if framework_info == None:
        fail("TargetFramework {} was not fetched for pkg dep {}. Fetched tfms: {}.".format(
            tfm,
            pkg.name,
            ", ".join([k for k, v in pkg.frameworks.items()]),
        ))
    files.append(framework_info.all_dep_files)
