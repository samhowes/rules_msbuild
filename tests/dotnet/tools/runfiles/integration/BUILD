load("@io_bazel_rules_go//go:def.bzl", "go_test")
load("//dotnet:defs.bzl", "msbuild_binary")

msbuild_binary(
    name = "DotnetCat",
    srcs = ["DotnetCat.cs"],
    data = ["foo.txt"],
    target_framework = "netcoreapp3.1",
    visibility = ["//visibility:public"],
    deps = [
        "//dotnet/tools/Runfiles",
    ],
)

genrule(
    name = "run_dotnet_cat",
    srcs = [],
    outs = ["run_dotnet_cat_result.txt"],
    cmd = "$(location :DotnetCat) > $@",
    tools = [":DotnetCat"],
)

go_test(
    name = "integration_test",
    size = "small",
    srcs = ["dotnet_cat_test.go"],
    data = [
        ":DotnetCat",
        ":run_dotnet_cat",
    ],
    deps = [
        "//tests/tools/executable",
        "//tests/tools/files",
        "@com_github_stretchr_testify//assert",
    ],
)
