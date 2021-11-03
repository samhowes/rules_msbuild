# Public definitions for Dotnet rules.
#
# All public Dotnet rules, providers, and other definitions are imported and
# re-exported in this file. This allows the real location of definitions
# to change for easier maintenance.
#
# Definitions outside this file are private unless otherwise noted, and
# may change without notice.

load(
    "@rules_msbuild//dotnet/private:msbuild_macros.bzl",
    "msbuild_binary_macro",
    "msbuild_directory_macro",
    "msbuild_library_macro",
    "msbuild_test_macro",
)
load(
    "@rules_msbuild//dotnet/private/toolchain:nuget.bzl",
    _nuget_deps_helper = "nuget_deps_helper",
    _nuget_fetch = "nuget_fetch",
)

BAZEL_VERSION = "4.1.0"

# primary end-user rules
msbuild_directory = msbuild_directory_macro
msbuild_binary = msbuild_binary_macro
msbuild_library = msbuild_library_macro
msbuild_test = msbuild_test_macro

# nuget
nuget_fetch = _nuget_fetch
nuget_deps_helper = _nuget_deps_helper
