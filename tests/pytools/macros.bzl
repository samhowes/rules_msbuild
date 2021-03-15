load("@rules_python//python:defs.bzl", "py_test")

def py_build_test(target):
    name = target + "_test"
    py_test(
        name = name,
        srcs = [name + ".py"],
        env = {
            "DOTNET_BUILD_TARGET": "$(rootpath :{})".format(target),
        },
        data = [":" + target],
        deps = [
            "//tests/pytools:build_test_case",
        ],
    )
