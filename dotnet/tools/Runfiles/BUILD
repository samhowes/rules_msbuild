load("//dotnet:defs.bzl", "msbuild_library")

msbuild_library(
    name = "Runfiles",
    srcs = [
        "BazelEnvironment.cs",
        "Check.cs",
        "Label.cs",
        "LabelRunfiles.cs",
        "Runfiles.cs",
    ],
    project_file = "Runfiles.csproj",
    target_framework = "netstandard2.1",
    visibility = ["//visibility:public"],
)
