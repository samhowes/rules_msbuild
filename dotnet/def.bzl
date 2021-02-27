
"""Public definitions for Dotnet rules.

All public Dotnet rules, providers, and other definitions are imported and
re-exported in this file. This allows the real location of definitions
to change for easier maintenance.

Definitions outside this file are private unless otherwise noted, and
may change without notice.
"""

load(
    "//dotnet/private:dotnet_toolchain.bzl",
    _declare_toolchains = "declare_toolchains",
    _dotnet_toolchain = "dotnet_toolchain",
)
load(
    "//dotnet/private/rules:sdk.bzl",
    _dotnet_sdk = "dotnet_sdk"
)

declare_toolchains = _declare_toolchains
dotnet_toolchain = _dotnet_toolchain
dotnet_sdk = _dotnet_sdk