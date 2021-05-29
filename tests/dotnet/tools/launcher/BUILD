load("@io_bazel_rules_go//go:def.bzl", "go_test")
load("@my_rules_dotnet//dotnet:defs.bzl", "msbuild_binary")

msbuild_binary(
    name = "Greeter",
    srcs = ["Greeter.cs"],
    project_file = "Greeter.csproj",
    target_framework = "netcoreapp3.1",
)

genrule(
    name = "run_greeter",
    srcs = [],
    outs = ["run_greeter.txt"],
    cmd = "$(location :Greeter) 'genrule' > $@",
    exec_tools = [":Greeter"],
)

go_test(
    name = "launcher_test",
    srcs = ["launcher_test.go"],
    data = [
        ":Greeter",
        ":run_greeter",
    ],
    deps = [
        "//tests/tools/executable",
        "//tests/tools/files",
        "@com_github_stretchr_testify//assert",
    ],
)
