load("//dotnet:defs.bzl", "dotnet_test")

dotnet_test(
    name = "BuilderTest",
    size = "small",
    srcs = glob(["*.cs"]),
    sdk = "Microsoft.NET.Sdk",
    target_framework = "netcoreapp3.1",
    visibility = ["//visibility:public"],
    deps = [
        "@dotnet_sdk//:builder",
        "@nuget//FluentAssertions",
        "@nuget//Microsoft.NET.Test.Sdk",
        "@nuget//xunit",
        "@nuget//xunit.runner.visualstudio",
    ],
)
