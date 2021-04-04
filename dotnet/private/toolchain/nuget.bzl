load("@bazel_skylib//lib:paths.bzl", "paths")
load("@my_rules_dotnet//dotnet/private:xml.bzl", "import_sdk", "inline_element", "prepare_nuget_config", "prepare_restore_file", "project_references")
load("@my_rules_dotnet//dotnet/private/actions:common.bzl", "make_dotnet_cmd")
load("@my_rules_dotnet//dotnet/private/toolchain:sdk.bzl", "detect_host_platform")
load("@my_rules_dotnet//dotnet/private:providers.bzl", "DEFAULT_SDK", "MSBuildSdk")

def _nuget_fetch_impl(ctx):
    dotnet_path = ctx.path(ctx.attr.dotnet_bin)
    fetch_project = _generate_fetch_project(ctx, ctx.attr.packages)
    os, _ = detect_host_platform(ctx)

    args, env, _ = make_dotnet_cmd(
        str(dotnet_path.dirname),
        os,
        str(fetch_project),
        "restore",
        True,  # todo
    )
    args = [dotnet_path] + args
    print(args)
    result = ctx.execute(
        args,
        environment = env,
        quiet = False,
        working_directory = str(fetch_project.dirname),
    )
    if result.return_code != 0:
        fail(result.stdout)
    print(result.stdout)

def _generate_fetch_project(ctx, packages):
    build_traversal = MSBuildSdk(name = "Microsoft.Build.Traversal", version = "3.0.3")
    path = "/".join([build_traversal.name.lower(), build_traversal.version])
    packages_folder = str(ctx.path("packages"))
    ctx.download_and_extract(
        url = "https://www.nuget.org/api/v2/package/" + path,
        output = paths.join(packages_folder, path),
        type = "zip",
        sha256 = "b68b7e98843b1ecd499e43a34bb62d8e7a033a285e9a976688675f402d92aa0f",
    )

    substitutions = prepare_nuget_config(
        packages_folder,
        True,
        # todo(#46) allow custom packages
        {"nuget.org": "https://api.nuget.org/v3/index.json"},
    )
    nuget_config_file = "NuGet.Fetch.Config"
    ctx.template(
        ctx.path(nuget_config_file),
        ctx.attr._fetch_config,
        substitutions = substitutions,
    )

    packages_by_framework = {}
    for spec, frameworks in packages.items():
        # todo(#4) validate spec
        pkg, version = spec.split(":")
        for tfm in frameworks:
            pkg_dict = packages_by_framework.setdefault(tfm, {})

            if pkg in pkg_dict:
                fail("Multiple package versions are not supported.")

            pkg_dict[pkg] = struct(name = pkg, version = version)

    project_names = []
    for tfm, pkgs in packages_by_framework.items():
        proj_name = tfm + ".proj"
        project_names.append(proj_name)
        substitutions = prepare_restore_file(
            DEFAULT_SDK,
            paths.join("_obj", tfm),
            [],
            pkgs.values(),
            nuget_config_file,  # nuget config will be specified at the top level
            tfm,
        )
        ctx.template(
            ctx.path(proj_name),
            ctx.attr._tfm_template,
            substitutions = substitutions,
        )

    substitutions = prepare_restore_file(
        build_traversal,
        paths.join("_obj", "traversal"),
        project_references(project_names),
        [],
        nuget_config_file,
        None,  # no tfm for the traversal project
    )
    fetch_project = ctx.path("nuget.fetch.proj")
    ctx.template(
        fetch_project,
        ctx.attr._master_template,
        substitutions = substitutions,
    )
    return fetch_project

nuget_fetch = repository_rule(
    implementation = _nuget_fetch_impl,
    attrs = {
        "packages": attr.string_list_dict(),
        "dotnet_bin": attr.label(
            default = Label("@dotnet_sdk//:dotnet"),
            allow_single_file = True,
            executable = True,
            cfg = "exec",
        ),
        "_master_template": attr.label(
            default = Label("@my_rules_dotnet//dotnet/private/msbuild:restore.tpl.proj"),
        ),
        "_tfm_template": attr.label(
            default = Label("@my_rules_dotnet//dotnet/private/msbuild:restore.tpl.proj"),
        ),
        "_fetch_config": attr.label(
            default = Label("@my_rules_dotnet//dotnet/private/msbuild:NuGet.Fetch.config"),
            allow_single_file = True,
        ),
    },
)
