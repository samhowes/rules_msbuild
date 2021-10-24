load("@bazel_skylib//lib:dicts.bzl", "dicts")
load("@bazel_skylib//lib:paths.bzl", "paths")
load(
    "//dotnet/private/rules:msbuild.bzl",
    "msbuild_binary",
    "msbuild_library",
    "msbuild_publish",
    "msbuild_restore",
    "msbuild_test",
)
load(
    "//dotnet/private/rules:nuget.bzl",
    "nuget_package",
)

def msbuild_directory_macro(
        name = "msbuild_directory",
        srcs = [],
        deps = []):
    msbuild_project(name, srcs, deps, [":__subpackages__"])

def msbuild_project(
        name,
        srcs = [],
        deps = [],
        visibility = ["//visibility:private"]):
    native.filegroup(
        name = name,
        srcs = srcs + deps,
        visibility = visibility,
    )

def msbuild_binary_macro(
        name,
        srcs = None,
        args = [],
        project_file = None,
        target_framework = None,
        deps = [],
        **kwargs):
    srcs = _get_srcs(srcs)
    project_file = _guess_project_file(name, srcs, project_file)
    _msbuild_assembly(name, msbuild_binary, project_file, target_framework, srcs, deps, kwargs, {"args": args})

    msbuild_publish(
        name = name + "_publish",
        project_file = project_file,
        target = ":" + name,
    )

def msbuild_library_macro(
        name,
        srcs = [],
        project_file = None,
        target_framework = None,
        deps = [],
        **kwargs):
    _msbuild_assembly(name, msbuild_library, project_file, target_framework, srcs, deps, kwargs, {})

def msbuild_test_macro(
        name,
        srcs = [],
        project_file = None,
        target_framework = None,
        deps = [],
        **kwargs):
    test_args = _steal_args({}, kwargs, ["size", "dotnet_cmd", "test_env"])

    _msbuild_assembly(name, msbuild_test, project_file, target_framework, srcs, deps, kwargs, test_args)

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
        assembly_args):
    _steal_args(assembly_args, kwargs, ["data", "content", "protos"])

    srcs = _get_srcs(srcs)
    project_file = _guess_project_file(name, srcs, project_file)
    restore_name = name + "_restore"

    restore_deps = []
    for d in deps:
        l = Label(d)
        restore_deps.append(l.relative(":{}_restore".format(l.name)))

    is_packable = kwargs.pop("packable", False)
    version = kwargs.pop("version", None)
    package_version = kwargs.pop("package_version", version)
    visibility = kwargs.get("visibility", None)

    msbuild_restore(
        name = restore_name,
        target_framework = target_framework,
        project_file = project_file,
        deps = restore_deps,
        **dicts.add(kwargs, dict(
            version = version,
            package_version = package_version,
        ))
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

    if is_packable:
        nuget_package(
            name = name + ".nupkg",
            project_file = project_file,
            version = package_version,
            target = ":" + name + "_publish",
            visibility = visibility,
        )

def _steal_args(dest, src, args):
    for a in args:
        if a in src:
            val = src.pop(a)
            dest[a] = val
    return dest

def _get_srcs(srcs):
    if srcs != None:
        return srcs
    return native.glob(
        ["**/*"],
        exclude = [
            "bin/**",
            "obj/**",
            "*proj",
            "BUILD*",
        ],
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
