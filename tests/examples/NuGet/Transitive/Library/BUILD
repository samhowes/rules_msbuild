load("//dotnet:defs.bzl", "dotnet_library")
load("//tests/tools:build_test.bzl", "library_test")

dotnet_library(
    name = "Library",
    srcs = ["JsonParser.cs"],
    target_framework = "netcoreapp3.1",
    visibility = ["//tests/examples/NuGet/Transitive:__subpackages__"],
    deps = [
        "@nuget//newtonsoft.json",
    ],
)

library_test(
    name = "Library_test",
    expected_files = {
        "netcoreapp3.1": [
            "Library.dll",
            "!Newtonsoft.Json.dll",
        ],
    },
    target = "Library",
)
