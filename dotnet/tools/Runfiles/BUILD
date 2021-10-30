load("//dotnet:defs.bzl", "msbuild_library")
load("//:version.bzl", "VERSION")

msbuild_library(
    name = "Runfiles",
    srcs = [
        "BazelEnvironment.cs",
        "Check.cs",
        "Label.cs",
        "LabelRunfiles.cs",
        "Runfiles.cs",
    ],
    packable = True,
    package_id = "RulesMSBuild.Runfiles",
    project_file = "Runfiles.csproj",
    target_framework = "netstandard2.1",
    version = VERSION,
    visibility = ["//visibility:public"],
)
