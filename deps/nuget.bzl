load("@rules_msbuild//deps:public_nuget.bzl", "FRAMEWORKS", "PACKAGES")
load("@rules_msbuild//dotnet:defs.bzl", "nuget_deps_helper", "nuget_fetch")

def nuget_deps():
    nuget_fetch(
        name = "nuget",
        packages = {
            "FluentAssertions/5.10.3": ["net5.0", "net6.0", "netcoreapp3.1"],
            "Grpc.AspNetCore/2.40.0": ["net6.0"],
            "Grpc.Net.Client/2.40.0": ["net6.0"],
            "Microsoft.Build.Tasks.Core/17.0.0": ["net6.0"],
            "Microsoft.Build.Utilities.Core/17.0.0": ["net6.0"],
            "Microsoft.NET.StringTools/1.0.0": ["net6.0"],
            "Microsoft.NET.Test.Sdk/16.7.1": ["net5.0", "net6.0", "netcoreapp3.1"],
            "Moq/4.16.1": ["net5.0", "net6.0"],
            "Newtonsoft.Json/13.0.1": ["net6.0"],
            "SamHowes.Microsoft.Build/17.0.0": ["net6.0"],
            "SharpZipLib/1.3.3": ["net6.0"],
            "System.Security.Principal/4.3.0": ["net6.0"],
            "coverlet.collector/1.3.0": ["net5.0"],
            "xunit.assert/2.4.1": ["net6.0"],
            "xunit.console/2.4.1": ["net6.0"],
            "xunit.core/2.4.1": ["net6.0"],
            "xunit.extensibility.core/2.4.1": ["net6.0"],
            "xunit.runner.visualstudio/2.4.3": ["net5.0", "net6.0", "netcoreapp3.1"],
            "xunit/2.4.1": ["net5.0", "net6.0", "netcoreapp3.1"],
        },
        target_frameworks = ["net5.0", "net6.0", "netcoreapp3.1", "netstandard2.1"],
        use_host = True,
        deps = nuget_deps_helper(FRAMEWORKS, PACKAGES),
    )
