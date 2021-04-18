load("@io_bazel_rules_go//go:def.bzl", "go_test")
load("//dotnet:defs.bzl", "dotnet_binary")

dotnet_binary(
    name = "dotnet_cat",
    srcs = ["DotnetCat.cs"],
    data = ["foo.txt"],
    target_framework = "netcoreapp3.1",
    deps = ["//dotnet/tools/runfiles"],
)

genrule(
    name = "run_dotnet_cat",
    srcs = [],
    outs = ["run_dotnet_cat_result.txt"],
    cmd = "$(location :dotnet_cat) > $@",
    tools = [":dotnet_cat"],
)

go_test(
    name = "integration_test",
    srcs = ["dotnet_cat_test.go"],
    data = [
        ":dotnet_cat",
        ":run_dotnet_cat",
    ],
    deps = ["//tests/tools/lib"],
)
