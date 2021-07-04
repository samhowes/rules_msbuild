load(
    "//dotnet/private:repositories.bzl",
    _dotnet_rules_repositories = "dotnet_rules_repositories",
)
load(
    "//dotnet/private/toolchain:sdk.bzl",
    _dotnet_register_toolchains = "dotnet_register_toolchains",
)

dotnet_rules_repositories = _dotnet_rules_repositories
dotnet_register_toolchains = _dotnet_register_toolchains
