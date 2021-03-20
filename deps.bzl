load("@rules_python//python:pip.bzl", "pip_install")

def py_deps():
    # Create a central repo that knows about the dependencies needed for
    # requirements.txt.
    pip_install(
        name = "pip",
        requirements = "//py:requirements.txt",
    )
