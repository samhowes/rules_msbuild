"""A Fake implementation of nuget_import for Bootstrapping"""

load("//dotnet/private/nuget:rules.bzl", "nuget_import")

def fake_nuget_import(name):
    nuget_import(
        name = name,
        package_name = name,
        version = "0.0.0",
    )
