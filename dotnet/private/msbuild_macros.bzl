load("@bazel_skylib//lib:dicts.bzl", "dicts")
load("@bazel_skylib//lib:paths.bzl", "paths")
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
    project_file = _guess_project_file(name, srcs, project_file)
    _msbuild_assembly(name, _msbuild_binary, project_file, target_framework, srcs, deps, kwargs)

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
    _msbuild_assembly(name, _msbuild_library, project_file, target_framework, srcs, deps, kwargs)

def msbuild_test(
        name,
        project_file = None,
        target_framework = None,
        srcs = [],
        deps = [],
        **kwargs):
    test_args = dict(
        size = kwargs.pop("size", None),
    )

    _msbuild_assembly(name, _msbuild_test, project_file, target_framework, srcs, deps, kwargs, test_args)

_KNOWN_EXTS = {
    ".cs": True,
    ".fs": True,
    ".vb": True,
}

def _msbuild_assembly(
        name,
        assembly_impl,
        project_file,
        target_framework,
        srcs,
        deps,
        kwargs,
        assembly_args = {}):
    assembly_args = dicts.add(assembly_args, dict(
        [
            [k, kwargs.pop(k, None)]
            for k in ["data"]
        ],
    ))

    project_file = _guess_project_file(name, srcs, project_file)
    restore_name = name + "_restore"

    restore_deps = []
    for d in deps:
        l = Label(d)
        restore_deps.append(l.relative(":{}_restore".format(l.name)))

    msbuild_restore(
        name = restore_name,
        target_framework = target_framework,
        project_file = project_file,
        deps = restore_deps,
        **kwargs
    )

    assembly_impl(
        name = name,
        srcs = srcs,
        target_framework = target_framework,
        project_file = project_file,
        restore = ":" + restore_name,
        deps = deps,
        **dicts.add(kwargs, assembly_args)
    )

def _guess_project_file(name, srcs, project_file):
    if project_file != None:
        return project_file

    for src in srcs:
        _, ext = paths.split_extension(src)
        if _KNOWN_EXTS.get(ext, False):
            return name + ext + "proj"

    if project_file == None:
        fail("Target {} is missing project_file attribute and has no srcs to determine project type from. " +
             "Please directly specify project_file or add a source file with one of the following " +
             "extensions: {}".format(name, ", ".join(_KNOWN_EXTS.keys())))
