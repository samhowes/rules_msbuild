
"""Public definitions for Dotnet rules.

All public Dotnet rules, providers, and other definitions are imported and
re-exported in this file. This allows the real location of definitions
to change for easier maintenance.

Definitions outside this file are private unless otherwise noted, and
may change without notice.
"""
load(
    "//dotnet/private:providers.bzl",
    _DotnetSdkInfo = "DotnetSdkInfo",
)
load(
    "//dotnet/private:dotnet_toolchain.bzl",
    _declare_toolchains = "declare_toolchains",
    _dotnet_toolchain = "dotnet_toolchain",
)
load(
    "//dotnet/private/rules:sdk.bzl",
    _dotnet_sdk = "dotnet_sdk"
)
load(
    "//dotnet/private/rules:binary.bzl",
    _dotnet_binary = "dotnet_binary",
)
load(
    "//dotnet/private:context.bzl",
    _dotnet_context = "dotnet_context",
)

declare_toolchains = _declare_toolchains
dotnet_toolchain = _dotnet_toolchain
dotnet_sdk = _dotnet_sdk
dotnet_context = _dotnet_context

# See dotnet/providers.md#DotnetSdkInfo for full documentation.
DotnetSdkInfo = _DotnetSdkInfo

# See dotnet/core.md#dotnet_binary for full documentation.
dotnet_binary = _dotnet_binary
