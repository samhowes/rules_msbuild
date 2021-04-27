load("//dotnet:defs.bzl", "dotnet_test")

dotnet_test(
    name = "RunfilesTests",
    size = "small",
    srcs = glob(["*.cs"]),
    sdk = "Microsoft.NET.Sdk",
    target_framework = "netcoreapp3.1",
    visibility = ["//visibility:public"],
    deps = [
        "//dotnet/tools/runfiles",  # keep
        "@dotnet_sdk//:builder",  # keep
        "@nuget//FluentAssertions",
        "@nuget//Microsoft.NET.Test.Sdk",
        "@nuget//xunit",
        "@nuget//xunit.runner.visualstudio",
    ],
)
