load("//dotnet:defs.bzl", "dotnet_binary")

genrule(
    name = "foo",
    srcs = [],
    outs = ["foo.txt"],
    cmd = "echo \"Hello Runfiles!\" > $@",
)

dotnet_binary(
    name = "dotnet_cat",
    srcs = ["DotnetCat.cs"],
    data = [":foo"],
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

py_test(
    name = "dotnet_cat_test",
    size = "small",
    srcs = ["dotnet_cat_test.py"],
    data = [
        ":dotnet_cat",
        ":run_dotnet_cat",
    ],
    env = {
        "RUN_RESULT": "$(location :run_dotnet_cat)",
        "TARGET_BINARY": "$(location :dotnet_cat)",
    },
    deps = [
        "//tests/tools:executable",
        "//tests/tools:mypytest",
        "@rules_python//python/runfiles",
    ],
)
