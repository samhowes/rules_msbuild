load(
    "//dotnet/private:repositories.bzl",
    _msbuild_rules_dependencies = "msbuild_rules_dependencies",
)
load(
    "//dotnet/private/toolchain:sdk.bzl",
    _msbuild_register_toolchains = "msbuild_register_toolchains",
)

msbuild_rules_dependencies = _msbuild_rules_dependencies
msbuild_register_toolchains = _msbuild_register_toolchains
