"""Repository rules to define dotnet_toolchain"""
load("//dotnet/private:platforms.bzl", "PLATFORMS")
load("//dotnet/private:providers.bzl", "DotnetSdkInfo")
load("//dotnet/private/actions:binary.bzl", "emit_binary")

def _dotnet_toolchain_impl(ctx):
    sdk = ctx.attr.sdk[DotnetSdkInfo]
    cross_compile = ctx.attr.dotnetos != sdk.dotnetos or ctx.attr.dotnetarch != sdk.dotnetarch
    return [platform_common.ToolchainInfo(
        # Public fields
        name = ctx.label.name,
        cross_compile = cross_compile,
        default_dotnetos = ctx.attr.dotnetos,
        default_dotnetarch = ctx.attr.dotnetarch,
        actions = struct(
            binary = emit_binary,
        ),
        flags = struct(
            compile = (),
        ),
        sdk = sdk,

        # Internal fields -- may be read by emit functions.
        _builder = ctx.executable.builder,
    )]

dotnet_toolchain = rule(
    _dotnet_toolchain_impl,
    attrs = {
        # Minimum requirements to specify a toolchain
        "builder": attr.label(
            mandatory = True,
            cfg = "exec",
            executable = True,
            doc = "Tool used to execute most Dotnet actions",
        ),
        "dotnetos": attr.string(
            mandatory = True,
            doc = "Default target OS",
        ),
        "dotnetarch": attr.string(
            mandatory = True,
            doc = "Default target architecture",
        ),
        "sdk": attr.label(
            mandatory = True,
            providers = [DotnetSdkInfo],
            cfg = "exec",
            doc = "The SDK this toolchain is based on",
        ),
    },
    doc = "Defines a Dotnet toolchain based on an SDK",
    provides = [platform_common.ToolchainInfo],
)

def declare_toolchains(host, sdk, builder):
    """Declares dotnet_toolchain and toolchain targets for each platform."""

    # keep in sync with generate_toolchain_names
    host_dotnetos, _, host_dotnetarch = host.partition("_")
    for p in PLATFORMS:
        toolchain_name = "dotnet_" + p.name
        impl_name = toolchain_name + "-impl"

        constraints = p.constraints

        dotnet_toolchain(
            name = impl_name,
            dotnetos = p.dotnetos,
            dotnetarch = p.dotnetarch,
            sdk = sdk,
            builder = builder,
            tags = ["manual"],
            visibility = ["//visibility:public"],
        )
        native.toolchain(
            name = toolchain_name,
            toolchain_type = "@my_rules_dotnet//dotnet:toolchain",
            exec_compatible_with = [
                "@my_rules_dotnet//dotnet/toolchain:" + host_dotnetos,
                "@my_rules_dotnet//dotnet/toolchain:" + host_dotnetarch,
            ],
            target_compatible_with = constraints,
            toolchain = ":" + impl_name,
        )
