load("@rules_msbuild//dotnet:defs.bzl", "msbuild_binary")

msbuild_binary(
    name = "NuGetParser",
    srcs = [
        "AssetsReader.cs",
        "BazelLogger.cs",
        "BuildGenerator.cs",
        "BuildWriter.cs",
        "Extensions.cs",
        "Package.cs",
        "PackageId.cs",
        "Parser.cs",
        "Program.cs",
        "ProjectBuilder.cs",
        "Restorer.cs",
        "TfmParser.cs",
    ],
    target_framework = "netcoreapp3.1",  # keep
    visibility = ["//visibility:public"],
)

filegroup(
    name = "files",
    srcs = glob(["*"]),
)
