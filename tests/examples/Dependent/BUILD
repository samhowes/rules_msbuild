load("//tests/tools/build_test:build_test.bzl", "build_test")
load("//dotnet:defs.bzl", "msbuild_binary")

msbuild_binary(
    name = "Dependent",
    srcs = glob(["**/*.cs"]),
    project_file = "Dependent.csproj",
    target_framework = "net6.0",
    visibility = ["//visibility:public"],
    deps = [
        "//tests/examples/ClassLibrary",
    ],
)

build_test(
    name = "Dependent_test",
    expected_files = {
        "net6.0": [
            "Dependent.dll",
            "ClassLibrary.pdb",
            "ClassLibrary.dll",
        ],
    },
    expected_output = "Hello Secret: Foo\n",
)

build_test(
    name = "Dependent_publish_test",
    expected_files = {
        "publish/net6.0": [
            "Dependent.dll",
            "ClassLibrary.pdb",
            "ClassLibrary.dll",
        ],
    },
)
