load(":providers.bzl", "DotnetContextInfo")
load(":common.bzl", "dotnetos_to_extension", "dotnetos_to_shared_extension")

def _dotnet_context_data_impl(ctx):
    providers = [
        DotnetContextInfo(),
    ]
    return providers

dotnet_context_data = rule(
    _dotnet_context_data_impl,
    attrs = {},
    doc = """dotnet_context_data gathers information about the build configuration.
    It is a common dependency of all Dotnet targets.""",
    toolchains = ["@my_rules_dotnet//dotnet:toolchain"],
)


def dotnet_context(ctx, attr = None):
    """Returns an API used to build Dotnet code.

    See /dotnet/toolchains.md#dotnet-context"""
    if not attr:
        attr = ctx.attr
    toolchain = ctx.toolchains["@my_rules_dotnet//dotnet:toolchain"]
    if hasattr(attr, "_dotnet_context_data"):
        pass

    env = {
        "DOTNETARCH": toolchain.default_dotnetos,
        "DOTNETOS": toolchain.default_dotnetarch,
    }

    dotnetos = toolchain.default_dotnetos
    
    return struct(
        # Fields
        toolchain = toolchain,
        sdk = toolchain.sdk,
        dotnet = toolchain.sdk.dotnet,
        sdk_root = toolchain.sdk.root_file,
        actions = ctx.actions,
        exe_extension = dotnetos_to_extension(dotnetos),
        shared_extension = dotnetos_to_shared_extension(dotnetos),
        env = env,
        
        # Action generators
        binary = toolchain.actions.binary,
        
        # Helpers
        _ctx = ctx
    )