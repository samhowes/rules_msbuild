load(
    "//dotnet/private/rules:msbuild.bzl",
    "msbuild_publish",
    "msbuild_restore",
    _msbuild_binary = "msbuild_binary",
    _msbuild_library = "msbuild_library",
    _msbuild_test = "msbuild_test",
)

def msbuild_binary(
        name,
        project_file = None,
        target_framework = None,
        srcs = [],
        deps = [],
        **kwargs):
    _msbuild_assembly(name, _msbuild_binary, project_file, target_framework, srcs, deps, **kwargs)

    msbuild_publish(
        name = name + "_publish",
        project_file = project_file,
        target = ":" + name,
    )

def msbuild_library(
        name,
        project_file = None,
        target_framework = None,
        srcs = [],
        deps = [],
        **kwargs):
    _msbuild_assembly(name, _msbuild_library, project_file, target_framework, srcs, deps, **kwargs)

def msbuild_test(
        name,
        project_file = None,
        target_framework = None,
        srcs = [],
        deps = [],
        **kwargs):
    _msbuild_assembly(name, _msbuild_test, project_file, target_framework, srcs, deps, **kwargs)

def _msbuild_assembly(
        name,
        assembly_impl,
        project_file,
        target_framework,
        srcs,
        deps,
        **kwargs):
    if project_file == None:
        fail("Target {} is missing required attribute 'project_file'".format(name))

    restore_name = name + "_restore"

    msbuild_restore(
        name = restore_name,
        target_framework = target_framework,
        project_file = project_file,
        deps = [d + "_restore" for d in deps],
        **kwargs
    )

    assembly_impl(
        name = name,
        srcs = srcs,
        target_framework = target_framework,
        project_file = project_file,
        restore = ":" + restore_name,
        deps = deps,
        **kwargs
    )
