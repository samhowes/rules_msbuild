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
load("//dotnet/private/rules:directory.bzl", "msbuild_directory")
load("//dotnet/private/rules:nuget.bzl", "nuget_package")

def msbuild_directory_macro(
        name = None,
        srcs = [],
        deps = [],
        visibility = None,
        **kwargs):
    if visibility == None:
        visibility = ["%s//:__subpackages__" % native.repository_name()]

    msbuild_directory(
        name = name,
        srcs = srcs,
        deps = deps,
        visibility = visibility,
        **kwargs
    )

def msbuild_project(
        name,
        srcs = [],
        deps = [],
        visibility = ["//visibility:private"],
        **kwargs):
    native.filegroup(
        name = name,
        srcs = srcs + deps,
        visibility = visibility,
        **kwargs
    )

def msbuild_binary_macro(
        name,
        args = [],
        **kwargs):
    _msbuild_assembly(name, msbuild_binary, kwargs, {"args": args})

def msbuild_library_macro(
        name,
        **kwargs):
    _msbuild_assembly(name, msbuild_library, kwargs, {})

def msbuild_test_macro(
        name,
        **kwargs):
    test_args = _steal_args({}, kwargs, ["size", "dotnet_cmd", "test_env"])

    _msbuild_assembly(name, msbuild_test, kwargs, test_args)

_KNOWN_EXTS = {
    ".cs": True,
    ".fs": True,
    ".vb": True,
}

def _msbuild_assembly(
        name,
        assembly_impl,
        kwargs,
        assembly_args):
    _steal_args(assembly_args, kwargs, ["data", "content", "protos"])

    srcs, project_file = _guess_inputs(name, kwargs)

    kwargs.setdefault("msbuild_directory", "%s//:msbuild_defaults" % native.repository_name())
    deps = kwargs.pop("deps", [])
    target_framework = kwargs.pop("target_framework", None)
    restore_deps = []
    for d in deps:
        l = Label(d)
        rel = str(l.relative(":{}_restore".format(l.name)))
        if rel[0] == "@" and d[0] != "@":
            rel = "//" + rel.split("//")[1]
        restore_deps.append(rel)

    is_packable = kwargs.pop("packable", False)
    package_id = kwargs.pop("package_id", name)
    version = kwargs.pop("version", None)
    package_version = kwargs.pop("package_version", version)
    visibility = kwargs.get("visibility", None)

    restore_name = name + "_restore"
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
            package_id = package_id,
            version = package_version,
            target = ":" + name + "_publish",
            visibility = visibility,
        )

    if assembly_impl != msbuild_test:
        msbuild_publish(
            name = name + "_publish",
            project_file = project_file,
            target = ":" + name,
        )

def _steal_args(dest, src, args):
    for a in args:
        if a in src:
            val = src.pop(a)
            dest[a] = val
    return dest

def _guess_inputs(name, kwargs):
    srcs = kwargs.pop("srcs", None)
    project_file = kwargs.pop("project_file", None)
    lang = kwargs.pop("lang", "")
    lang_ext = ""
    if lang:
        lang_ext = "." + lang

    if project_file != None:
        _, ext = paths.split_extension(project_file)
        lang_ext = ext[:3]
        lang = lang_ext[1:]
    else:
        if srcs == None:
            srcs = native.glob(
                ["**/*" + lang_ext],
                exclude = [
                    "bin/**",
                    "obj/**",
                    "*proj",
                    "BUILD*",
                ],
            )
        if lang_ext:
            project_file = name + lang_ext + "proj"
        else:
            for src in srcs:
                _, lang_ext = paths.split_extension(src)
                if _KNOWN_EXTS.get(lang_ext, False):
                    project_file = name + lang_ext + "proj"
                    lang = lang_ext[1:]
                    break

        if project_file == None:
            target = "//{}:{}" % (native.package(), name)
            langs = ", ".join(_KNOWN_EXTS.keys())
            fail("Target {} is missing oneof (project_file, srcs, lang) attributes and no source files were found to " +
                 "infer from. Please directly specify one of the attributes or add a source file with one of the " +
                 "following extensions: {}".format(target, langs))

    return srcs, project_file
