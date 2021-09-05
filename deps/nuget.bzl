load("@rules_msbuild//dotnet:defs.bzl", "nuget_fetch")

def nuget_deps():
    nuget_fetch(
        name = "nuget",
        use_host = True,
        target_frameworks = ["net5.0", "netcoreapp3.1", "netstandard2.1"],
        packages = {
            "CommandLineParser:2.9.0-preview1": ["netcoreapp3.1", "net5.0"],  # keep
            # test deps
            "FluentAssertions:5.10.3": ["netcoreapp3.1", "net5.0"],
            "Microsoft.Build.Locator:1.4.1": ["net5.0"],
            "Microsoft.Build.Tasks.Core:16.9.0": ["net5.0"],
            "Microsoft.Build.Utilities.Core:16.9.0": ["net5.0"],
            "Microsoft.NET.Test.Sdk:16.7.1": ["netcoreapp3.1", "net5.0"],
            "Moq:4.16.1": ["net5.0"],
            "Newtonsoft.Json:13.0.1": ["netcoreapp3.1"],  # keep
            "SamHowes.Microsoft.Build:16.9.0": ["net5.0"],
            "xunit:2.4.1": ["netcoreapp3.1"],
            "xunit.assert:2.4.1": ["net5.0"],
            "xunit.console:2.4.1": ["net5.0"],
            "xunit.core:2.4.1": ["net5.0"],
            "xunit.extensibility.core:2.4.1": ["net5.0"],
            "xunit.runner.visualstudio:2.4.3": ["netcoreapp3.1"],
        },
    )
