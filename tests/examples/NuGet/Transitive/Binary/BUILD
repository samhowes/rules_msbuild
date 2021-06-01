load("//tests/tools/build_test:build_test.bzl", "build_test")
load("//dotnet:defs.bzl", "msbuild_binary")

msbuild_binary(
    name = "Binary",
    srcs = ["Program.cs"],
    project_file = "Binary.csproj",
    target_framework = "netcoreapp3.1",
    visibility = ["//visibility:public"],
    deps = [
        "//tests/examples/NuGet/Transitive/Library",
    ],
)

build_test(
    name = "Binary_test",
    expected_files = {
        "netcoreapp3.1": [
            "Library.dll",
            "Newtonsoft.Json.dll",
        ],
    },
    expected_output = "bar\n",
)
