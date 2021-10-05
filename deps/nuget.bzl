load("@rules_msbuild//dotnet:defs.bzl", "nuget_deps_helper", "nuget_fetch")
load("@bazel_skylib//lib:dicts.bzl", "dicts")
load(":public_nuget.bzl", "FRAMEWORKS", "PACKAGES")

def nuget_deps():
    nuget_fetch(
        name = "nuget",
        use_host = True,
        target_frameworks = FRAMEWORKS,
        packages = {
            "CommandLineParser/2.8.0": ["netcoreapp3.1", "net5.0"],
            "FluentAssertions/5.10.3": ["netcoreapp3.1", "net5.0"],
            "Microsoft.NET.Test.Sdk/16.7.1": ["netcoreapp3.1", "net5.0"],
            "Moq/4.16.1": ["net5.0"],
            "Newtonsoft.Json/13.0.1": ["netcoreapp3.1", "netstandard1.3"],
            "xunit/2.4.1": ["netcoreapp3.1"],
            "xunit.assert/2.4.1": ["net5.0"],
            "xunit.console/2.4.1": ["net5.0"],
            "xunit.core/2.4.1": ["net5.0"],
            "xunit.extensibility.core/2.4.1": ["net5.0"],
            "xunit.runner.visualstudio/2.4.3": ["netcoreapp3.1"],
        },
        deps = nuget_deps_helper(FRAMEWORKS, PACKAGES),
    )
