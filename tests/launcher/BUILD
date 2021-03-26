load("@my_rules_dotnet//dotnet:defs.bzl", "dotnet_binary")

dotnet_binary(
    name = "Greeter",
    srcs = ["Greeter.cs"],
    target_framework = "netcoreapp3.1",
)

# this causes :Greeter to be compiled //bazel-out/host with its _own_ runfiles
# this allows us to test that the dotnet binary can be built and run as a user would run it with `bazel run`
# with the caveat that the working directory is in execroot, not runfiles/my_rules_dotnet
genrule(
    name = "run_greeter",
    srcs = [],
    outs = ["run_greeter.txt"],
    cmd = "$(location :Greeter) 'genrule' > $@",
    tools = [":Greeter"],
)

# a set of assertions that the launcher works in various scenarios
# 1) `bazel run` would work via checking the output of :run_greeter
#   $0 is relative to execroot
# 2) executed as a data dependency: it should use RUNFILES_DIR and RUNFILES_MANIFEST_FILE from environment
#   $0 is the result of rlocation in python
# 3) executed manually by the user in the directory next to Greeter.runfiles (accomplished via
#       constructing a fake runfiles directory/manifest file)
#   $0 is "./Greeter", the original binary
# 4) (non-windows only) executed manually by the user in Greeter's runfiles
#   $0 is "./Greeter", which is a symlink to the original binary
# 5) (non-windows only) executed manually in the runfiles of another binary
#   $0 is "./Greeter"
py_test(
    name = "launcher_test",
    srcs = ["launcher_test.py"],
    data = [
#        ":run_greeter",
        ":Greeter"
    ],
    deps = [
        "//tests/tools:mypytest",
        "//tests/tools:executable",
        "@rules_python//python/runfiles",
    ]
)
