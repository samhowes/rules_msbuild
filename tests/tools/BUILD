load("@rules_python//python:defs.bzl", "py_library")
load("@pip//:requirements.bzl", "requirement")

py_library(
    name = "mypytest",
    srcs = ["mypytest.py"],
    visibility = ["//visibility:public"],
    deps = [
        requirement("pytest"),
    ],
)

py_library(
    name = "build_test_case",
    srcs = ["build_test_case.py"],
    visibility = ["//visibility:public"],
    deps = [
        "@rules_python//python/runfiles",
    ],
)

py_library(
    name = "executable",
    srcs = ["executable.py"],
    visibility = ["//visibility:public"],
    deps = [],
)

exports_files(["build_test.py"])
