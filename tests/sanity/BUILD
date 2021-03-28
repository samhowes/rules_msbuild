# Simply a sanity test so I know that bazel is running and processing the workspace correctly

load("@io_bazel_rules_go//go:def.bzl", "go_binary", "go_library")

go_binary(
    name = "sanity",
    srcs = ["hello.go"],
    deps = [
        ":library",
    ],
)

go_library(
    name = "library",
    srcs = ["library.go"],
    importpath = "github.com/bazelbuild/my_rules_dotnet/tests/sanity",
)

go_binary(
    name = "Greeter",
    srcs = ["greeter.go"],
)

# copied from //tests/launcher/run_greeter
genrule(
    name = "run_greeter",
    srcs = [],
    outs = ["run_greeter.txt"],
    cmd = "$(location :Greeter) 'genrule' > $@",
    exec_tools = [":Greeter"],
)

# copied from //tests/launcher/launcher_test
py_test(
    name = "launcher_test",
    srcs = ["//tests/launcher:launcher_test.py"],
    data = [
        ":run_greeter",
        ":Greeter"
    ],
    deps = [
        "//tests/tools:mypytest",
        "//tests/tools:executable",
        "@rules_python//python/runfiles",
    ],
    env = {
        "TARGET_PATH": "tests/sanity/Greeter_"
    }
)