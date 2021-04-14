load("@my_rules_dotnet//dotnet:defs.bzl", "nuget_fetch")

def nuget_deps():
    nuget_fetch(
        name = "nuget",
        packages = {
            # stay at v16.7.1: https://github.com/dotnet/sdk/issues/16860
            "Microsoft.NET.Test.Sdk:16.7.1": ["netcoreapp3.1"],
            "CommandLineParser:2.9.0-preview1": ["netcoreapp3.1"],
            "xunit:2.4.1": ["netcoreapp3.1"],
            "xunit.runner.visualstudio:2.4.3": ["netcoreapp3.1"],
            "newtonsoft.json:13.0.1": ["netcoreapp3.1"],
        },
    )
