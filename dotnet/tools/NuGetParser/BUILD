load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_binary", "dotnet_publish")

dotnet_binary(
    name = "NuGetParser",
    srcs = glob(["*.cs"]),
    msbuild_properties = {"RootNamespace": "NuGetParser"},
    sdk = "Microsoft.NET.Sdk",
    target_framework = "netcoreapp3.1",
    visibility = ["//visibility:public"],
)

dotnet_publish(
    name = "publish",
    target = ":NugetParser",
)
