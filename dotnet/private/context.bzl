load(":providers.bzl", "DotnetContextInfo")

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
