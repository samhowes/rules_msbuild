load("//dotnet/private/actions:assembly.bzl", "emit_assembly", "make_launcher")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo", "DotnetSdkInfo")
load("@bazel_skylib//lib:dicts.bzl", "dicts")

TFM_ATTR = attr.string(
    mandatory = True,
    doc = ("Target Framework Monikor (TFM) for the target .NET Framework i.e. netcoreapp3.1" +
           " https://docs.microsoft.com/en-us/dotnet/standard/frameworks"),
)
DEPS_ATTR = attr.label_list(
    providers = [DotnetLibraryInfo],
)

# Used by dotnet_tool_binary
BASE_ASSEMBLY_ATTRS = {
    "srcs": attr.label_list(allow_files = [".cs"]),
    "target_framework": TFM_ATTR,
    "_compile_template": attr.label(
        default = Label("//dotnet/private/rules:compile.tpl.proj"),
        allow_single_file = True,
    ),
    "_restore_template": attr.label(
        default = Label("//dotnet/private/msbuild:restore.tpl.proj"),
        allow_single_file = True,
    ),
}

ASSEMBLY_ATTRS = dicts.add(BASE_ASSEMBLY_ATTRS, {
    "_dotnet_context_data": attr.label(default = "//:dotnet_context_data"),
    "data": attr.label_list(allow_files = True),
    "deps": DEPS_ATTR,
})

def _dotnet_tool_binary_impl(ctx):
    sdk = ctx.attr.sdk[DotnetSdkInfo]
    assembly, pdb, outputs = emit_assembly(ctx, sdk, True)
    return [
        DefaultInfo(
            files = depset(outputs),
            executable = assembly,
        ),
    ]

def _dotnet_binary_impl(ctx):
    toolchain = ctx.toolchains["@my_rules_dotnet//dotnet:toolchain"]
    assembly, pdb, outputs = emit_assembly(ctx, toolchain.sdk, True)

    launcher = make_launcher(ctx, toolchain, assembly)

    launcher_info = ctx.attr._launcher_template[DefaultInfo]
    assembly_runfiles = ctx.runfiles(files = (
        toolchain.sdk.all_files +
        outputs +
        ctx.files.data
    ))
    assembly_runfiles = assembly_runfiles.merge(launcher_info.default_runfiles)
    return [
        DefaultInfo(
            files = depset(outputs),
            runfiles = assembly_runfiles,
            executable = launcher,
        ),
    ]

def _dotnet_library_impl(ctx):
    toolchain = ctx.toolchains["@my_rules_dotnet//dotnet:toolchain"]
    library, pdb, outputs = emit_assembly(ctx, toolchain.sdk, False)
    return [
        DefaultInfo(
            files = depset(outputs),
        ),
        DotnetLibraryInfo(
            assembly = library,
            pdb = pdb,
            deps = depset(
                direct = [dep[DotnetLibraryInfo] for dep in ctx.attr.deps],
                transitive = [dep[DotnetLibraryInfo].deps for dep in ctx.attr.deps],
            ),
        ),
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
    attrs = dicts.add(ASSEMBLY_ATTRS, {
        "_launcher_template": attr.label(
            default = Label("//dotnet/tools/launcher"),
            allow_single_file = True,
        ),
    }),
    executable = True,
    toolchains = ["@my_rules_dotnet//dotnet:toolchain"],
)

dotnet_library = rule(
    _dotnet_library_impl,
    attrs = ASSEMBLY_ATTRS,
    executable = False,
    toolchains = ["@my_rules_dotnet//dotnet:toolchain"],
)
