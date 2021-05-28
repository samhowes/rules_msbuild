load("//dotnet/private:providers.bzl", "DotnetLibraryInfo", "DotnetRestoreInfo", "NuGetPackageInfo")
load("//dotnet/private:context.bzl", "dotnet_context", "dotnet_exec_context")
load("//dotnet/private/actions:restore.bzl", "restore")
load("//dotnet/private/actions:publish.bzl", "publish")
load("//dotnet/private/actions:msbuild_assembly.bzl", "build_assembly")
load("//dotnet/private/actions:launcher.bzl", "make_launcher")
load("@bazel_skylib//lib:dicts.bzl", "dicts")

def _publish_impl(ctx):
    output_dir = publish(ctx)
    all = depset([output_dir])
    return [
        DefaultInfo(files = all),
        OutputGroupInfo(all = all),
    ]

def _restore_impl(ctx):
    dotnet = dotnet_exec_context(ctx, False)
    restore_info, outputs = restore(ctx, dotnet)
    return [
        DefaultInfo(
            files = depset([restore_info.restore_dir]),
        ),
        restore_info,
        OutputGroupInfo(
            all = depset(outputs),
        ),
    ]

def _binary_impl(ctx):
    dotnet = dotnet_exec_context(ctx, True)
    info, outputs = build_assembly(ctx, dotnet)
    launcher = make_launcher(ctx, dotnet, info)

    launcher_info = ctx.attr._launcher_template[DefaultInfo]
    assembly_runfiles = ctx.runfiles(
        files = [info.output_dir] + ctx.files.data,
        transitive_files = depset([dotnet.sdk.dotnet]),
    )

    assembly_runfiles = assembly_runfiles.merge(launcher_info.default_runfiles)

    return [
        DefaultInfo(
            files = depset([launcher, info.assembly]),
            runfiles = assembly_runfiles,
            executable = launcher,
        ),
        info,
        OutputGroupInfo(
            all = outputs,
        ),
    ]

def _library_impl(ctx):
    dotnet = dotnet_exec_context(ctx, False)
    info, outputs = build_assembly(ctx, dotnet)
    return [
        DefaultInfo(
            files = depset([info.assembly]),
            # todo(#6) add runfiles
        ),
        info,
        OutputGroupInfo(
            all = outputs,
        ),
    ]

def _test_impl(ctx):
    pass

_TOOLCHAINS = ["@my_rules_dotnet//dotnet:toolchain"]
_COMMON_ATTRS = {
    "project_file": attr.label(allow_single_file = True, mandatory = True),
}

msbuild_publish = rule(
    _publish_impl,
    attrs = dicts.add(_COMMON_ATTRS, {
        "target": attr.label(mandatory = True, providers = [DotnetLibraryInfo]),
    }),
    executable = False,
    toolchains = _TOOLCHAINS,
)

_RESTORE_ATTRS = dicts.add(_COMMON_ATTRS, {
    "target_framework": attr.string(mandatory = True),
})

msbuild_restore = rule(
    _restore_impl,
    attrs = dicts.add(_RESTORE_ATTRS, {
        "deps": attr.label_list(providers = [
            [DotnetRestoreInfo],
            [NuGetPackageInfo],
        ]),
    }),
    executable = False,
    toolchains = _TOOLCHAINS,
)

_ASSEMBLY_ATTRS = dicts.add(_RESTORE_ATTRS, {
    "srcs": attr.label_list(allow_files = True),
    "restore": attr.label(mandatory = True, providers = [DotnetRestoreInfo]),
    "data": attr.label_list(allow_files = True),
    "deps": attr.label_list(providers = [
        [DotnetLibraryInfo],
        [NuGetPackageInfo],
    ]),
})

_EXECUTABLE_ATTRS = dicts.add(_ASSEMBLY_ATTRS, {
    "_launcher_template": attr.label(
        default = Label("//dotnet/tools/launcher"),
        allow_single_file = True,
    ),
})

msbuild_binary = rule(
    _binary_impl,
    attrs = _EXECUTABLE_ATTRS,
    executable = True,
    toolchains = _TOOLCHAINS,
)

msbuild_library = rule(
    _library_impl,
    attrs = _ASSEMBLY_ATTRS,
    executable = False,
    toolchains = _TOOLCHAINS,
)

msbuild_test = rule(
    _test_impl,
    attrs = _ASSEMBLY_ATTRS,
    executable = True,
    test = True,
    toolchains = _TOOLCHAINS,
)
