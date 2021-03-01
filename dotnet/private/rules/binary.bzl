load("//dotnet/private/actions:assembly.bzl", "emit_assembly")
load("//dotnet/private/rules:common.bzl", "ASSEMBLY_ATTRS")

def _dotnet_binary_impl(ctx):
    """dotnet_binary_impl emits actions for compiling dotnet binaries"""
    executable, pdb, outputs = emit_assembly(ctx, True)
    return [
        DefaultInfo(
            files = depset([executable.file]),
            # runfiles = dotnet._ctx.runfiles(files=[proj]),
            runfiles = None,
            executable = executable.file,
        ),
    ]

_dotnet_binary_kwargs = {
    "implementation": _dotnet_binary_impl,
    "attrs": ASSEMBLY_ATTRS,
    "executable": True,
    "toolchains": ["@my_rules_dotnet//dotnet:toolchain"],
}

dotnet_binary = rule(**_dotnet_binary_kwargs)
    