load("@rules_python//python:defs.bzl", "py_library")

py_library(
    name = "build_test_case",
    srcs = ["build_test_case.py"],
    visibility = ["//visibility:public"],
    deps = [
        "@rules_python//python/runfiles",
    ],
)

exports_files(["build_test.py"])
