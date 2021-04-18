load("@bazel_skylib//lib:dicts.bzl", "dicts")
load("@io_bazel_rules_go//go:def.bzl", "go_binary", "go_library", "go_test")

def build_test(name, target, expected_files, args = [], expected_output = ""):
    artifacts = target + "_artifacts"
    native.filegroup(
        name = artifacts,
        srcs = [":" + target],
        output_group = "all",
        testonly = True,
    )

    test_config_name = name + "_gen"
    test_config(
        name = test_config_name,
        expected_output = expected_output,
        args = args,
        target = target,
        json = json.encode({"expectedFiles": expected_files}),
        deps = [":" + target],
        visibility = ["//visibility:public"],
        testonly = True,
    )

    go_test(
        name = name,
        size = "small",
        srcs = [":" + test_config_name],
        data = [
            ":" + target,
            ":" + artifacts,
        ],
        # make locating artifacts simple and not need runfiles library (as much)
        rundir = select({
            # windows doesn't have a symlink farm, cd to the artifact directory
            # by default, on windows bazel starts us in <pkg_path>/target_/target.exe.runfiles/worksapce_name
            # we want to start in <pkg_path>. Go compiles things into the target_ directory
            "@bazel_tools//src/conditions:host_windows": "../../..",
            # unix has a symlink farm and the default is nice: its the artifact directory
            "//conditions:default": "",

        }),
        deps = [
            "//tests/tools/executable",
            "//tests/tools/files",
            "@io_bazel_rules_go//go/tools/bazel:go_default_library",
        ],
    )

def _test_config_impl(ctx):
    f = ctx.actions.declare_file(ctx.attr.name.rsplit("_", 1)[0] + ".go")

    ctx.actions.expand_template(
        template = ctx.file._test_template,
        output = f,
        is_executable = False,
        substitutions = {
            "%target%": ctx.expand_location("$(location {})".format(ctx.attr.target.label)),
            "%args%": json.encode(ctx.attr.args),
            "%expected_output%": ctx.attr.expected_output,
            "%config_json%": ctx.attr.json,
            "%exec_path%": ctx.expand_location("$(execpath {})".format(ctx.attr.target.label)),
        },
    )

    return [DefaultInfo(files = depset([f]))]

test_config = rule(
    _test_config_impl,
    attrs = {
        "target": attr.label(),
        "args": attr.string_list(),
        "expected_output": attr.string(),
        "json": attr.string(),
        "deps": attr.label_list(),
        "_test_template": attr.label(
            allow_single_file = True,
            default = Label("//tests/tools/build_test:build_test.go"),
        ),
    },
)
