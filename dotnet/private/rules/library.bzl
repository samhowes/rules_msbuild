load("//dotnet/private/actions:assembly.bzl", "emit_assembly")
load("//dotnet/private/rules:common.bzl", "ASSEMBLY_ATTRS")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo")


def _dotnet_library_impl(ctx):
    """dotnet_library_impl emits actions for compiling dotnet code"""
    library, outputs = emit_assembly(ctx, False)
    return [
        DefaultInfo(files=depset([library.output])),
        DotnetLibraryInfo(
            assembly = library,
            deps = depset()
        ),
    ]

dotnet_library = rule( 
    _dotnet_library_impl,
    attrs = ASSEMBLY_ATTRS,
    executable = False,
    toolchains = ["@my_rules_dotnet//dotnet:toolchain"],
)
    