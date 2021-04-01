load("//dotnet/private/actions:assembly.bzl", "emit_assembly")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo")
load("@bazel_skylib//lib:dicts.bzl", "dicts")

TFM_ATTR = attr.string(
    mandatory = True,
    doc = ("Target Framework Monikor (TFM) for the target .NET Framework i.e. netcoreapp3.1" +
           " https://docs.microsoft.com/en-us/dotnet/standard/frameworks"),
)
DEPS_ATTR = attr.label_list(
    providers = [DotnetLibraryInfo],
)
ASSEMBLY_ATTRS = {
    "srcs": attr.label_list(allow_files = [".cs"]),
    "target_framework": TFM_ATTR,
    "data": attr.label_list(allow_files = True),
    "deps": DEPS_ATTR,
    "_compile_template": attr.label(
        default = Label("//dotnet/private/rules:compile.tpl.proj"),
        allow_single_file = True,
    ),
    "_restore_template": attr.label(
        default = Label("//dotnet/private/rules:restore.tpl.proj"),
        allow_single_file = True,
    ),
    "_dotnet_context_data": attr.label(default = "//:dotnet_context_data"),
}

def _dotnet_binary_impl(ctx):
    """dotnet_binary_impl emits actions for compiling dotnet binaries"""
    assembly, pdb, outputs, launcher = emit_assembly(ctx, True)

    launcher_info = ctx.attr._launcher_template[DefaultInfo]
    assembly_runfiles = ctx.runfiles(files = (
        ctx.toolchains["@my_rules_dotnet//dotnet:toolchain"].sdk.all_files +
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
    """dotnet_library_impl emits actions for compiling a dotnet library"""
    library, pdb, outputs, _ = emit_assembly(ctx, False)
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

dotnet_binary = rule(
    implementation = _dotnet_binary_impl,
    attrs = dicts.add(ASSEMBLY_ATTRS, {
        "_launcher_template": attr.label(
            default = Label("//dotnet/private/launcher:launcher_unix"),
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
