"""Xml Helpers"""

load("@bazel_skylib//lib:paths.bzl", "paths")
load("@bazel_skylib//lib:dicts.bzl", "dicts")
load("//dotnet/private:providers.bzl", "MSBuildSdk")

INTERMEDIATE_BASE = "obj"
STARTUP_DIR = "$(MSBuildStartupDirectory)"
EXEC_ROOT = "$(ExecRoot)"
THIS_DIR = "$(MSBuildThisFileDirectory)"

def properties(property_dict):
    return "\n    ".join([
        element(k, v)
        for k, v in property_dict.items()
    ])

def element(name, value, attrs = {}):
    open_tag_items = [name]
    open_tag_items.extend(
        [
            '{}="{}"'.format(k, v)
            for k, v in attrs.items()
        ],
    )
    return "<{open_tag}>{value}</{name}>".format(
        name = name,
        open_tag = " ".join(open_tag_items),
        value = value,
    )

def inline_element(name, attrs):
    attr_strings = [
        "{}=\"{}\"".format(a, attrs[a])
        for a in attrs
    ]
    return "<{name} {attrs} />".format(name = name, attrs = " ".join(attr_strings))

def _import_sdk(name, project_type, version = None):
    attrs = {
        "Project": "Sdk." + project_type,
        "Sdk": name,
    }
    if version != None:
        attrs["Version"] = version
    return inline_element("Import", attrs)

def import_sdk(name, version = None):
    return (
        _import_sdk(name, "props", version),
        _import_sdk(name, "targets", version),
    )

def make_project_file(ctx, dotnet, dep_files, exec_root = EXEC_ROOT):
    (intermediate_path, nuget_config, is_executable) = dotnet.config.intermediate_path, dotnet.sdk.config.nuget_config, dotnet.config.is_executable
    post_sdk_properties = dicts.add(getattr(ctx.attr, "msbuild_properties", {}))
    if is_executable:
        post_sdk_properties["OutputType"] = "Exe"
        post_sdk_properties["UseAppHost"] = "false"

    nuget_config_path = paths.join(exec_root, nuget_config.path)
    pre_sdk_properties = {
        # we'll take care of making sure deps are built, no need to traverse
        "BuildProjectReferences": "false",
        "RestoreRecursive": "false",
    }
    source_project_file = getattr(ctx.file, "project_file", None)
    substitutionas = None
    if source_project_file != None:
        pre_sdk_properties["TargetFramework"] = ctx.attr.target_framework
        substitutions = prepare_project_file(
            None,
            intermediate_path,
            [],
            [],
            nuget_config_path,
            None,
            pre_sdk_properties = pre_sdk_properties,
            post_sdk_properties = post_sdk_properties,
            srcs = ctx.files.srcs,
            imports = [source_project_file],
            exec_root = exec_root,
        )
    else:
        substitutions = prepare_project_file(
            MSBuildSdk(ctx.attr.sdk, None),
            intermediate_path,
            [paths.join(EXEC_ROOT, r.path) for r in dep_files.references],
            dep_files.packages,
            nuget_config_path,
            tfm = ctx.attr.target_framework,
            pre_sdk_properties = pre_sdk_properties,
            post_sdk_properties = post_sdk_properties,
            srcs = ctx.files.srcs,
            exec_root = exec_root,
        )

    project_file = ctx.actions.declare_file(ctx.label.name + ".csproj")
    ctx.actions.expand_template(
        template = ctx.file._project_template,
        output = project_file,
        is_executable = False,
        substitutions = substitutions,
    )
    return project_file

def prepare_project_file(
        msbuild_sdk,
        intermediate_path,
        references,
        packages,
        nuget_config_path,
        tfm = None,
        pre_sdk_properties = {},
        post_sdk_properties = {},
        srcs = [],
        imports = [],
        exec_root = EXEC_ROOT):
    pre_import_msbuild_properties = dicts.add(pre_sdk_properties, {
        "RestoreConfigFile": nuget_config_path,
        # this is where nuget creates project.assets.json (and other files) during a restore
        "BaseIntermediateOutputPath": THIS_DIR + intermediate_path,
        "IntermediateOutputPath": paths.join(THIS_DIR, intermediate_path),
        # https://docs.microsoft.com/en-us/visualstudio/msbuild/customize-your-build?view=vs-2019#msbuildprojectextensionspath
        # this is where nuget looks for a project.assets.json during a build
        "MSBuildProjectExtensionsPath": THIS_DIR + intermediate_path,
        # we could just set ProjectAssetsFile here, but we're setting the other properties in case they have other impacts
        "OutputPath": THIS_DIR + paths.dirname(intermediate_path),
        #"ImportDirectoryBuildProps": "false",
        "UseSharedCompilation": "false",
    })

    post_import_msbuild_properties = dicts.add(post_sdk_properties, {})

    if tfm != None:
        post_import_msbuild_properties["TargetFramework"] = tfm

    compile_srcs = [
        inline_element("Compile", {"Include": paths.join(exec_root, src.path)})
        for src in srcs
    ]

    project_references = [
        inline_element("ProjectReference", {"Include": path})
        for path in references
    ]

    package_references = [
        inline_element(
            "PackageReference",
            {
                "Include": p.name,
                "Version": p.version,
            },
        )
        for p in packages
    ]

    props, targets = [], []
    if msbuild_sdk != None:
        p, t = import_sdk(msbuild_sdk.name, msbuild_sdk.version)
        props.append(p)
        targets.append(t)
    else:
        props = [inline_element("Import", {"Project": paths.join(exec_root, i.path)}) for i in imports]

    sep = "\n    "
    substitutions = {
        "{pre_import_msbuild_properties}": properties(pre_import_msbuild_properties),
        "{sdk_props}": sep.join(props),
        "{post_import_msbuild_properties}": properties(post_import_msbuild_properties),
        "{compile_srcs}": sep.join(compile_srcs),
        "{references}": sep.join(project_references),
        "{package_references}": sep.join(package_references),
        "{sdk_targets}": sep.join(targets),
    }

    return substitutions

def prepare_nuget_config(packages_folder, restore_enabled, package_sources):
    sources = []
    for name, url in package_sources.items():
        attrs = {"key": name, "value": url}

        # todo(#46)
        if True:
            attrs["protocolVersion"] = "3"
        sources.append(inline_element("add", attrs))

    return {
        "{packages_folder}": packages_folder,
        "{restore_enabled}": str(restore_enabled),
        "{package_sources}": "\n    ".join(sources),
    }
