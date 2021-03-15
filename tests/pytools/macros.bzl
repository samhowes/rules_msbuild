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

def build_test(name, args = [], expected_output = "", expected_files = []):
    target = name.rsplit("_", 1)[0]
    src = "//tests/pytools:build_test.py"
    py_test(
        name = name,
        main = src,
        srcs = [src],
        env = {
            "DOTNET_BUILD_TARGET": "$(rootpath :{})".format(target),
            "DOTNET_BUILD_TARGET_ARGS": ";".join(args),
            "DOTNET_BUILD_EXPECTED_OUTPUT": expected_output,
            "DOTNET_BUILD_EXPECTED_FILES": ";".join(expected_files),
        },
        data = [":" + target],
        deps = [
            "//tests/pytools:build_test_case",
        ],
    )
