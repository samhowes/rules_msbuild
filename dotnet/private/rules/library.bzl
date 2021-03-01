load("//dotnet/private/actions:assembly.bzl", "emit_assembly")
load("//dotnet/private/rules:common.bzl", "ASSEMBLY_ATTRS")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo")


def _dotnet_library_impl(ctx):
    """dotnet_library_impl emits actions for compiling a dotnet library"""
    library, pdb, outputs = emit_assembly(ctx, False)
    return [
        DefaultInfo(files=depset(outputs)),
        DotnetLibraryInfo(
            assembly = library.file,
            pdb = pdb.file,
            deps = depset()
        ),
    ]

dotnet_library = rule( 
    _dotnet_library_impl,
    attrs = ASSEMBLY_ATTRS,
    executable = False,
    toolchains = ["@my_rules_dotnet//dotnet:toolchain"],
)
    