load("//dotnet/private/actions:assembly.bzl", "emit_assembly", "make_launcher")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo", "DotnetSdkInfo", "NuGetPackageInfo")
load("//dotnet/private:context.bzl", "dotnet_context", "dotnet_exec_context")
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
    dotnet = dotnet_exec_context(ctx, True)

    info, outputs, private = emit_assembly(ctx, dotnet)
    return [DefaultInfo(
        files = depset([info.assembly]),
        executable = info.assembly,
    ), OutputGroupInfo(
        all = depset(outputs + private),
    )]

def _make_executable(ctx, test):
    dotnet = dotnet_exec_context(ctx, True, test)

    info, outputs, private = emit_assembly(ctx, dotnet)
    launcher = make_launcher(ctx, dotnet, info)

    tfm_runtime = dotnet.sdk.shared[dotnet.sdk.config.tfm_mapping[ctx.attr.target_framework]]

    launcher_info = ctx.attr._launcher_template[DefaultInfo]
    assembly_runfiles = ctx.runfiles(
        files = ctx.files.data + private,
        transitive_files = depset(
            [info.output_dir],
            transitive = [
                tfm_runtime,
                dotnet.sdk.all_files,
            ],
        ),
    )
    assembly_runfiles = assembly_runfiles.merge(launcher_info.default_runfiles)
    return [
        DefaultInfo(
            files = depset([launcher]),
            runfiles = assembly_runfiles,
            executable = launcher,
        ),
        OutputGroupInfo(
            all = depset(outputs + private),
        ),
    ]

def _dotnet_binary_impl(ctx):
    return _make_executable(ctx, False)

def _dotnet_test_impl(ctx):
    return _make_executable(ctx, True)

def _dotnet_library_impl(ctx):
    dotnet = dotnet_exec_context(ctx, False)
    info, outputs, private = emit_assembly(ctx, dotnet)
    return [
        DefaultInfo(
            files = depset([info.assembly]),
        ),
        OutputGroupInfo(
            all = depset(outputs + private),
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
