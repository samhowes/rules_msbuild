load("@my_rules_dotnet//dotnet:defs.bzl", "nuget_fetch")

def nuget_deps():
    nuget_fetch(
        name = "nuget",
        packages = {
            "CommandLineParser:2.9.0-preview1": ["netcoreapp3.1"],
            "Newtonsoft.Json:13.0.1": ["netcoreapp3.1", "netstandard2.0"],
        },
    )
