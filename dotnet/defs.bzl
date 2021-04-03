"""Public definitions for Dotnet rules.

All public Dotnet rules, providers, and other definitions are imported and
re-exported in this file. This allows the real location of definitions
to change for easier maintenance.

Definitions outside this file are private unless otherwise noted, and
may change without notice.
"""

load(
    "//dotnet/private:providers.bzl",
    _DotnetLibraryInfo = "DotnetLibraryInfo",
    _DotnetSdkInfo = "DotnetSdkInfo",
    _NuGetPackageInfo = "NuGetPackageInfo",
)
load(
    "//dotnet/private:dotnet_toolchain.bzl",
    _declare_toolchains = "declare_toolchains",
    _dotnet_toolchain = "dotnet_toolchain",
)
load(
    "//dotnet/private/rules:sdk.bzl",
    _dotnet_sdk = "dotnet_sdk",
)
load(
    "//dotnet/private/rules:core.bzl",
    _dotnet_binary = "dotnet_binary",
    _dotnet_library = "dotnet_library",
    _dotnet_tool_binary = "dotnet_tool_binary",
)
load(
    "//dotnet/private/nuget:repository.bzl",
    _nuget_config = "nuget_config",
)
load(
    "//dotnet/private/rules:nuget.bzl",
    _nuget_import = "nuget_import",
    # _nuget_restore = "nuget_restore",
)

declare_toolchains = _declare_toolchains
dotnet_toolchain = _dotnet_toolchain
dotnet_sdk = _dotnet_sdk

nuget_config = _nuget_config

# nuget_restore = _nuget_restore
nuget_import = _nuget_import

# See dotnet/providers.md#DotnetSdkInfo for full documentation.
DotnetSdkInfo = _DotnetSdkInfo
DotnetLibraryInfo = _DotnetLibraryInfo
NuGetPackageInfo = _NuGetPackageInfo

dotnet_binary = _dotnet_binary
dotnet_tool_binary = _dotnet_tool_binary
dotnet_library = _dotnet_library
