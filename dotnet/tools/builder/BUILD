load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_binary", "dotnet_publish")
load("//dotnet:defs.bzl", "dotnet_tool_binary")

filegroup(
    name = "builder_srcs",
    srcs = [
        "Builder.cs",
        "LaunchDataWriter.cs",
        "LauncherFactory.cs",
        "OutputProcessor.cs",
        "ProcessorContext.cs",
    ],
    visibility = ["//visibility:public"],
)

dotnet_binary(
    name = "Builder",
    srcs = glob(["*.cs"]),
    sdk = "Microsoft.NET.Sdk",
    target_framework = "netcoreapp3.1",
    visibility = ["//visibility:public"],
)

dotnet_publish(
    name = "publish",
    target = ":Builder",
)
