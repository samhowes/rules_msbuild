load("@rules_python//python:pip.bzl", "pip_install")

def py_deps():
    pip_install(
        name = "pip",
        requirements = "//deps:requirements.txt",
    )
