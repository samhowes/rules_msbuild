load("//dotnet:defs.bzl", "msbuild_library")
load("//tests/tools/build_test:build_test.bzl", "build_test")

msbuild_library(
    name = "Library",
    srcs = ["JsonParser.cs"],
    project_file = "Library.csproj",
    target_framework = "netcoreapp3.1",
    visibility = ["//tests/examples/NuGet/Transitive:__subpackages__"],
    deps = [
        "@nuget//Newtonsoft.Json",
    ],
)

build_test(
    name = "Library_test",
    expected_files = {
        "netcoreapp3.1": [
            "Library.dll",
            "!Newtonsoft.Json.dll",
        ],
    },
)
