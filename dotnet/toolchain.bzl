load("@rules_dotnet_runtime//dotnet:defs.bzl", "DotnetPublishInfo", "DotnetRuntimeInfo")
load("//dotnet/private:providers.bzl", "DotnetSdkInfo")

def _impl(ctx):
    info = ctx.attr.builder[DotnetPublishInfo]
    executable = info.launcher if ctx.configuration.host_path_separator == ":" else info.launcher_windows

    return [platform_common.ToolchainInfo(
        builder = struct(
            files = info.files,
            executable = executable,
        ),
        dotnet_runtime = ctx.attr.dotnet_runtime[DotnetRuntimeInfo],
        dotnet_sdk = ctx.attr.dotnet_sdk[DotnetSdkInfo],
    )]

msbuild_toolchain = rule(
    _impl,
    attrs = {
        "builder": attr.label(
            mandatory = True,
            cfg = "host",
        ),
        "dotnet_runtime": attr.label(
            mandatory = True,
            providers = [DotnetRuntimeInfo],
        ),
    },
)
