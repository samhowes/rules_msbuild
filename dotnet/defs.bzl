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
    "//dotnet/private/toolchain:dotnet_toolchain.bzl",
    _declare_toolchains = "declare_toolchains",
    _dotnet_toolchain = "dotnet_toolchain",
)
load(
    "//dotnet/private/rules:sdk.bzl",
    _dotnet_config = "dotnet_config",
    _dotnet_sdk = "dotnet_sdk",
)
load(
    "//dotnet/private:msbuild_macros.bzl",
    "msbuild_binary_macro",
    "msbuild_directory_macro",
    "msbuild_library_macro",
    "msbuild_test_macro",
)
load("//dotnet/private/rules:msbuild.bzl", _msbuild_tool_binary = "msbuild_tool_binary")
load(
    "//dotnet/private/rules:nuget_download.bzl",
    _nuget_package_framework = "nuget_package_framework",
    _nuget_package_version = "nuget_package_version",
)
load(
    "//dotnet/private:nuget_macros.bzl",
    _nuget_package_download = "nuget_package_download",
)
load(
    "//dotnet/private/toolchain:nuget.bzl",
    _nuget_fetch = "nuget_fetch",
)

# primary end-user rules
msbuild_directory = msbuild_directory_macro
msbuild_binary = msbuild_binary_macro
msbuild_tool_binary = _msbuild_tool_binary
msbuild_library = msbuild_library_macro
msbuild_test = msbuild_test_macro

# repository rules
declare_toolchains = _declare_toolchains
dotnet_toolchain = _dotnet_toolchain
dotnet_sdk = _dotnet_sdk
dotnet_config = _dotnet_config
nuget_fetch = _nuget_fetch

# generated rules
nuget_package_download = _nuget_package_download
nuget_package_framework = _nuget_package_framework
nuget_package_version = _nuget_package_version

# providers
DotnetSdkInfo = _DotnetSdkInfo
DotnetLibraryInfo = _DotnetLibraryInfo
NuGetPackageInfo = _NuGetPackageInfo
