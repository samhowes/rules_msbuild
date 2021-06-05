load("@bazel_skylib//lib:dicts.bzl", "dicts")
load("@io_bazel_rules_go//go:def.bzl", "go_binary", "go_library", "go_test")

def build_test(name, expected_files, run_location = "", args = [], expected_output = ""):
    target = name.rsplit("_", 1)[0]
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
        run_location = run_location,
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
        deps = [
            "//tests/tools/executable",
            "//tests/tools/files",
            "@com_github_stretchr_testify//assert",
            "@io_bazel_rules_go//go/tools/bazel",
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
            "%run_location%": ctx.attr.run_location,
            "%compilation_mode%": ctx.var["COMPILATION_MODE"],
            "%package%": ctx.label.package,
        },
    )

    return [DefaultInfo(files = depset([f]))]

test_config = rule(
    _test_config_impl,
    attrs = {
        "target": attr.label(),
        "args": attr.string_list(),
        "expected_output": attr.string(),
        "run_location": attr.string(),
        "json": attr.string(),
        "deps": attr.label_list(),
        "_test_template": attr.label(
            allow_single_file = True,
            default = Label("//tests/tools/build_test:build_test.go"),
        ),
    },
)
