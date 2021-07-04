"""Repository rules to define dotnet_toolchain"""

load("//dotnet/private:platforms.bzl", "PLATFORMS")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo", "DotnetSdkInfo")

def _dotnet_toolchain_impl(ctx):
    sdk = ctx.attr.sdk[DotnetSdkInfo]
    cross_compile = ctx.attr.dotnetos != sdk.dotnetos or ctx.attr.dotnetarch != sdk.dotnetarch
    builder_info = ctx.attr.builder[DotnetLibraryInfo]
    return [platform_common.ToolchainInfo(
        # Public fields
        name = ctx.label.name,
        cross_compile = cross_compile,
        default_dotnetos = ctx.attr.dotnetos,
        default_dotnetarch = ctx.attr.dotnetarch,
        sdk = sdk,
        _builder = struct(
            assembly = builder_info.assembly,
            files = depset(
                [builder_info.output_dir],
                transitive = [sdk.runfiles],
            ),
        ),
    )]

dotnet_toolchain = rule(
    _dotnet_toolchain_impl,
    attrs = {
        "builder": attr.label(
            cfg = "exec",
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
            toolchain_type = "@rules_msbuild//dotnet:toolchain",
            exec_compatible_with = [
                "@rules_msbuild//dotnet:" + host_dotnetos,
                "@rules_msbuild//dotnet:" + host_dotnetarch,
            ],
            target_compatible_with = constraints,
            toolchain = ":" + impl_name,
        )
