load("//dotnet/tools/bazel_testing:bazel_integration_test.bzl", "rules_msbuild_integration_test")
load("//dotnet:defs.bzl", "BAZEL_VERSION")

def e2e_test(name):
    workspace_root = name[len("e2e_"):]
    srcs_name = "_%s_srcs" % name
    native.filegroup(
        name = srcs_name,
        srcs = native.glob([
            "%s/**/*" % workspace_root,
        ]),
    )

    rules_msbuild_integration_test(
        name = name,
        tags = ["e2e"],
        release = "//:release",
        workspace_files = srcs_name,
        bazel_binary = "@build_bazel_bazel_%s//:bazel_binary" % BAZEL_VERSION.replace(".", "_"),
    )
