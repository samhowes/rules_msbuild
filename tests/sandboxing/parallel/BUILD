load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_binary")

TAGS = [
    "manual",
    "no-cache",
    "no-remote",
]

# BUILD THIS TARGET
filegroup(
    name = "parallel",
    srcs = [
        ":foo",
        ":run_foo",
    ],
    tags = TAGS + ["exclusive"],
)

# DO NOT BUILD THIS TARGET
dotnet_binary(
    name = "foo",
    srcs = ["foo.cs"],
    tags = TAGS,
    target_framework = "netcoreapp3.1",
)

# DO NOT BUILD THIS TARGET
genrule(
    name = "run_foo",
    srcs = [],
    outs = ["run_foo.txt"],
    cmd = "$(location :foo) > $@",
    exec_tools = [":foo"],
    tags = TAGS,
)

# DO NOT ADD ANY OTHER TARGETS
