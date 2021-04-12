load("@my_rules_dotnet//dotnet:defs.bzl", "nuget_fetch")

def nuget_deps():
    nuget_fetch(
        name = "nuget",
        packages = {
            "CommandLineParser:2.9.0-preview1": ["netcoreapp3.1"],
            "xunit:2.4.1": ["netcoreapp3.1"],
            "xunit.runner.visualstudio:2.4.3": ["netcoreapp3.1"],
            "newtonsoft.json:13.0.1": ["netcoreapp3.1"],
        },
    )
