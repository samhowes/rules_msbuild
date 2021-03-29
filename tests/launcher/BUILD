load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_binary")

dotnet_binary(
    name = "Greeter",
    srcs = ["Greeter.cs"],
    target_framework = "netcoreapp3.1",
)

genrule(
    name = "run_greeter",
    srcs = [],
    outs = ["run_greeter.txt"],
    cmd = "$(location :Greeter) 'genrule' > $@",
    exec_tools = [":Greeter"],
)

py_test(
    name = "launcher_test",
    srcs = ["launcher_test.py"],
    data = [
        ":run_greeter",
        #        ":Greeter"
    ],
    env = {
        "TARGET_PATH": "tests/launcher/netcoreapp3.1",
    },
    deps = [
        "//tests/tools:executable",
        "//tests/tools:mypytest",
        "@rules_python//python/runfiles",
    ],
)
