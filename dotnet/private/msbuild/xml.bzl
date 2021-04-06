"""Xml Helpers"""

load("@bazel_skylib//lib:paths.bzl", "paths")

STARTUP_DIR = "$(MSBuildStartupDirectory)"
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
        _import_sdk(name, "Props", version),
        _import_sdk(name, "Targets", version),
    )

def project_references(project_paths):
    return [
        inline_element("ProjectReference", {"Include": path})
        for path in project_paths
    ]

def make_compile_file(ctx, is_executable, restore_file, libraries):
    msbuild_properties = [
    ]

    if is_executable:
        msbuild_properties.extend([
            element("OutputType", "Exe"),
            element("UseAppHost", "False"),
        ])

    compile_srcs = [
        inline_element("Compile", {"Include": paths.join(STARTUP_DIR, src.path)})
        for src in depset(ctx.files.srcs).to_list()
    ]

    references = [
        element(
            "Reference",
            element(
                "HintPath",
                paths.join(STARTUP_DIR, f.path),
            ),
            {
                "Include": paths.split_extension(
                    f.basename,
                )[0],
            },
        )
        for f in libraries
    ]

    # todo(#4) add package references

    compile_file = ctx.actions.declare_file(ctx.label.name + ".csproj")
    sep = "\n    "  # two indents of size 2
    ctx.actions.expand_template(
        template = ctx.file._compile_template,
        output = compile_file,
        is_executable = False,
        substitutions = {
            "{msbuild_properties}": sep.join(msbuild_properties),
            "{imports}": inline_element("Import", {"Project": restore_file.basename}),
            "{compile_srcs}": sep.join(compile_srcs),
            "{references}": sep.join(references),
        },
    )
    return compile_file

def prepare_restore_file(msbuild_sdk, intermediate_path, references, packages, nuget_config_path, tfm = None):
    pre_import_msbuild_properties = {
        "RestoreConfigFile": nuget_config_path,
        # this is where nuget creates project.assets.json (and other files) during a restore
        "BaseIntermediateOutputPath": THIS_DIR + intermediate_path,
        "IntermediateOutputPath": paths.join(THIS_DIR, intermediate_path),
        # https://docs.microsoft.com/en-us/visualstudio/msbuild/customize-your-build?view=vs-2019#msbuildprojectextensionspath
        # this is where nuget looks for a project.assets.json during a build
        "MSBuildProjectExtensionsPath": THIS_DIR + intermediate_path,
        # we could just set ProjectAssetsFile here, but we're setting the other properties in case they have other impacts
        "OutputPath": THIS_DIR + paths.dirname(intermediate_path),
        "ImportDirectoryBuildProps": "false",
        "UseSharedCompilation": "false",
    }

    post_import_msbuild_properties = {
    }

    if tfm != None:
        post_import_msbuild_properties["TargetFramework"] = tfm

    package_references = []
    package_sources = {}
    for p in packages:
        package_references.append(
            inline_element(
                "PackageReference",
                {
                    "Include": p.name,
                    "Version": p.version,
                },
            ),
        )

        # a package struct won't have workspace_name when we are fetching
        if hasattr(p, "packages_folder"):
            if p.packages_folder not in package_sources:
                package_sources[p.packages_folder] = paths.join(STARTUP_DIR, p.packages_folder)

    if len(package_sources) > 0:
        post_import_msbuild_properties["RestoreSources"] = ";\n".join(package_sources.values())

    props, targets = import_sdk(msbuild_sdk.name, msbuild_sdk.version)
    substitutions = {
        "{pre_import_msbuild_properties}": properties(pre_import_msbuild_properties),
        "{sdk_props}": props,
        "{post_import_msbuild_properties}": properties(post_import_msbuild_properties),
        "{references}": "\n    ".join(references),
        "{package_references}": "\n    ".join(package_references),
        "{sdk_targets}": targets,
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
