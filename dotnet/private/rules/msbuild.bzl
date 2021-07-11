load("//dotnet/private:providers.bzl", "DotnetLibraryInfo", "DotnetRestoreInfo", "DotnetSdkInfo", "NuGetPackageInfo")
load("//dotnet/private:context.bzl", "dotnet_exec_context")
load("//dotnet/private/actions:restore.bzl", "restore")
load("//dotnet/private/actions:publish.bzl", "publish")
load("//dotnet/private/actions:tool_binary.bzl", "build_tool_binary")
load("//dotnet/private/actions:assembly.bzl", "build_assembly")
load("//dotnet/private/actions:launcher.bzl", "make_launcher")
load("@bazel_skylib//lib:dicts.bzl", "dicts")

TOOLCHAINS = ["@rules_msbuild//dotnet:toolchain"]

def _msbuild_tool_binary_impl(ctx):
    dotnet = dotnet_exec_context(ctx, True)

    info, all_outputs = build_tool_binary(ctx, dotnet)
    return [
        DefaultInfo(
            files = depset([info.output_dir]),
        ),
        info,
        OutputGroupInfo(
            all = depset(all_outputs),
        ),
    ]

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
            files = depset([restore_info.output_dir]),
        ),
        restore_info,
        OutputGroupInfo(
            all = depset(outputs),
        ),
    ]

def _binary_impl(ctx):
    return _make_executable(ctx, False)

def _test_impl(ctx):
    return _make_executable(ctx, True)

def _make_executable(ctx, is_test):
    dotnet = dotnet_exec_context(ctx, True, is_test)
    info, outputs = build_assembly(ctx, dotnet)
    launcher = make_launcher(ctx, dotnet, info)

    launcher_info = ctx.attr._launcher_template[DefaultInfo]
    assembly_runfiles = ctx.runfiles(
        files = [info.output_dir] + ctx.files.data,
        transitive_files = depset([dotnet.sdk.dotnet], transitive = [info.runfiles]),
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
            runfiles = ctx.runfiles(transitive_files = info.runfiles),
        ),
        info,
        OutputGroupInfo(
            all = outputs,
        ),
    ]

_COMMON_ATTRS = {
    "project_file": attr.label(allow_single_file = True, mandatory = True),
}

msbuild_tool_binary = rule(
    implementation = _msbuild_tool_binary_impl,
    attrs = dicts.add(_COMMON_ATTRS, {
        "srcs": attr.label_list(allow_files = True),
        "target_framework": attr.string(),
        "dotnet_sdk": attr.label(
            mandatory = True,
            providers = [DotnetSdkInfo],
        ),
        "deps": attr.label_list(
            providers = [NuGetPackageInfo],
        ),
    }),
    # this is compiling a dotnet executable, but it'll be a framework dependent executable, so bazel won't be able
    # to execute it directly
    executable = False,
)

msbuild_publish = rule(
    _publish_impl,
    attrs = dicts.add(_COMMON_ATTRS, {
        "target": attr.label(mandatory = True, providers = [DotnetLibraryInfo]),
    }),
    executable = False,
    toolchains = TOOLCHAINS,
)

_RESTORE_ATTRS = dicts.add(_COMMON_ATTRS, {
    "msbuild_directory": attr.label(allow_files = True),
    "target_framework": attr.string(mandatory = True),
})

msbuild_restore = rule(
    _restore_impl,
    attrs = dicts.add(_RESTORE_ATTRS, {
        "deps": attr.label_list(providers = [
            [DotnetRestoreInfo],
            [NuGetPackageInfo],
        ]),
        "version": attr.string(),
        "package_version": attr.string(),
    }),
    executable = False,
    toolchains = TOOLCHAINS,
)

_ASSEMBLY_ATTRS = dicts.add(_RESTORE_ATTRS, {
    "srcs": attr.label_list(allow_files = True),
    "restore": attr.label(mandatory = True, providers = [DotnetRestoreInfo]),
    "data": attr.label_list(allow_files = True),
    "content": attr.label_list(allow_files = True),
    "deps": attr.label_list(providers = [
        [DotnetLibraryInfo],
        [NuGetPackageInfo],
    ]),
})

msbuild_library = rule(
    _library_impl,
    attrs = _ASSEMBLY_ATTRS,
    executable = False,
    toolchains = TOOLCHAINS,
)

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
    toolchains = TOOLCHAINS,
)

msbuild_test = rule(
    _test_impl,
    attrs = _EXECUTABLE_ATTRS,
    executable = True,
    test = True,
    toolchains = TOOLCHAINS,
)
