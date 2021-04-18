load("@bazel_skylib//lib:dicts.bzl", "dicts")
load("@rules_python//python:defs.bzl", "py_test")
load("@pip//:requirements.bzl", "requirement")
load("@rules_pkg//:pkg.bzl", "pkg_tar")

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
            "//tests/tools:build_test_case",
        ],
    )

def binary_test(name, target, args = [], expected_output = "", expected_files = {}):
    """Defines a py_test for a binary that asserts certain files are produced by the build

    Defines:
        name: the py_test
        target + _artifacts: a file group of output artifacts

    Args:
        name: the name of the py_test
        target: the name of the  binary target, assumes it is passed as the `name` attribute
            in the BUILD file where this macro is invoked
        args: a list of strings to be passed as arguments when invoking `target`
        expected_output: a string of the expected stdout when invoking the binary
        expected_files: a string_list_dict of directory path to file list of expected output files
            relative to the output base i.e. {'netcoreapp3.1': 'Binary.pdb'}
    """
    env = {
        "TARGET_ASSEMBLY_ARGS": ";".join(args),
        "EXPECTED_OUTPUT": expected_output,
    }

    _build_test(name, target, expected_files, env)

def library_test(name, target, expected_files = {}):
    """Defines a py_test for a dotnet_library that asserts certain files are produced by the build

    Defines:
        name: the py_test
        name + _artifacts: a file group of output artifacts

    Args:
        name: the name of the test
        target: the name of the dotnet_library target, assumes it is passed as the `name` attribute
            in the BUILD file where this macro is invoked
        expected_files: a string_list_dict of directory path to file list of expected output files
            relative to the output base i.e. {'netcoreapp3.1': 'Binary.pdb'}
    """
    env = {}
    _build_test(name, target, expected_files, env)

def _build_test(name, target, expected_files, env):
    artifacts = target + "_artifacts"
#    native.filegroup(
#        name = artifacts,
#        srcs = [":" + target],
#        output_group = "all",
#        testonly = True,
#    )

    # on windows the python zipper doesn't zip directories. Use a tar instead
    pkg_tar(
        name = artifacts,
        srcs = [":" + target],
        testonly = True,
        #    package_dir = "/usr/bin",
        #        srcs = [":dotnet_cat"],
    )

    env = dicts.add(env, {
        "TARGET_ASSEMBLY": "$(rootpath :{})".format(target),
        "TARBALL": "$(location :{})".format(artifacts),
        "EXPECTED_FILES": json.encode(expected_files),
        "DOTNET_LAUNCHER_DEBUG": "1",
    })

    src = "//tests/tools:build_test.py"
    py_test(
        name = name,
        main = src,
        srcs = [src],
        env = env,
        data = [
            ":" + target,
            ":" + artifacts,
        ],
        size = "small",
        deps = [
            "//tests/tools:mypytest",
            "//tests/tools:executable",
            "//tests/tools:build_test_case",
        ],
    )
