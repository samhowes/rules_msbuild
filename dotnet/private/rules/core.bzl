load("//dotnet/private/actions:assembly.bzl", "emit_assembly", "make_launcher")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo", "DotnetSdkInfo", "NuGetPackageInfo")
load("//dotnet/private:context.bzl", "dotnet_context")
load("@bazel_skylib//lib:dicts.bzl", "dicts")

TFM_ATTR = attr.string(
    mandatory = True,
    doc = ("Target Framework Monikor (TFM) for the target .NET Framework i.e. netcoreapp3.1" +
           " https://docs.microsoft.com/en-us/dotnet/standard/frameworks"),
)
DEPS_ATTR = attr.label_list(
    providers = [
        [DotnetLibraryInfo],
        [NuGetPackageInfo],
    ],
)

# Used by dotnet_tool_binary
BASE_ASSEMBLY_ATTRS = {
    "srcs": attr.label_list(allow_files = [".cs"]),
    "target_framework": TFM_ATTR,
    "_project_template": attr.label(
        default = Label("//dotnet/private/msbuild:project.tpl.proj"),
        allow_single_file = True,
    ),
}

ASSEMBLY_ATTRS = dicts.add(BASE_ASSEMBLY_ATTRS, {
    "_dotnet_context_data": attr.label(default = "//:dotnet_context_data"),
    "data": attr.label_list(allow_files = True),
    "deps": DEPS_ATTR,
})

EXECUTABLE_ATTRS = dicts.add(ASSEMBLY_ATTRS, {
    "_launcher_template": attr.label(
        default = Label("//dotnet/tools/launcher"),
        allow_single_file = True,
    ),
})

def _dotnet_tool_binary_impl(ctx):
    sdk = ctx.attr.sdk[DotnetSdkInfo]
    dotnet = dotnet_context(sdk.root_file.dirname, sdk.dotnetos, None, sdk)

    info, outputs, private = emit_assembly(ctx, dotnet, True)
    return [DefaultInfo(
        files = depset(outputs),
        executable = info.assembly,
    )]

def _primary_dotnet_context(ctx):
    toolchain = ctx.toolchains["@my_rules_dotnet//dotnet:toolchain"]

    return dotnet_context(
        toolchain.sdk.root_file.dirname,
        toolchain.sdk.dotnetos,
        toolchain._builder,
        toolchain.sdk,
    )

def _make_executable(ctx, test):
    dotnet = _primary_dotnet_context(ctx)

    info, outputs, private = emit_assembly(ctx, dotnet, True)

    dotnet_args = ["test"] if test else ["exec"]
    launcher = make_launcher(ctx, dotnet, info.assembly, dotnet_args)

    tfm_runtime = dotnet.sdk.shared[dotnet.sdk.config.tfm_mapping[ctx.attr.target_framework]]

    launcher_info = ctx.attr._launcher_template[DefaultInfo]
    assembly_runfiles = ctx.runfiles(
        files = ctx.files.data + private,
        transitive_files = depset(
            transitive = [
                tfm_runtime,
                dotnet.sdk.all_files,
                info.runtime,
            ],
        ),
    )
    assembly_runfiles = assembly_runfiles.merge(launcher_info.default_runfiles)
    return [
        DefaultInfo(
            files = depset(outputs),
            runfiles = assembly_runfiles,
            executable = launcher,
        ),
    ]

def _dotnet_binary_impl(ctx):
    return _make_executable(ctx, False)

def _dotnet_test_impl(ctx):
    return _make_executable(ctx, True)

def _dotnet_library_impl(ctx):
    dotnet = _primary_dotnet_context(ctx)
    info, outputs, private = emit_assembly(ctx, dotnet, False)
    return [
        DefaultInfo(
            files = depset(outputs),
        ),
        info,
    ]

dotnet_tool_binary = rule(
    implementation = _dotnet_tool_binary_impl,
    attrs = dicts.add(BASE_ASSEMBLY_ATTRS, {
        "sdk": attr.label(
            mandatory = True,
            providers = [DotnetSdkInfo],
        ),
    }),
    executable = True,
    doc = """Used instead of dotnet_binary for executables in the toolchain.

dotnet_tool_binaries cannot have any dependencies and are used to build other dotnet_* targets.""",
)

dotnet_binary = rule(
    implementation = _dotnet_binary_impl,
    attrs = EXECUTABLE_ATTRS,
    executable = True,
    toolchains = ["@my_rules_dotnet//dotnet:toolchain"],
)

dotnet_library = rule(
    _dotnet_library_impl,
    attrs = ASSEMBLY_ATTRS,
    executable = False,
    toolchains = ["@my_rules_dotnet//dotnet:toolchain"],
)

dotnet_test = rule(
    _dotnet_test_impl,
    attrs = EXECUTABLE_ATTRS,
    test = True,
    toolchains = ["@my_rules_dotnet//dotnet:toolchain"],
)
