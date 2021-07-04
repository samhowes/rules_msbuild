load("//dotnet/tools/bazel_testing:bazel_integration_test.bzl", "rules_msbuild_integration_test")
load("//dotnet:defs.bzl", "BAZEL_VERSION")

def e2e_test(name, **kwargs):
    workspace_root = name[len("e2e_"):]
    srcs_name = "_%s_srcs" % name
    native.filegroup(
        name = srcs_name,
        srcs = native.glob([
            "%s/**/*" % workspace_root,
        ]),
    )

    # Set tags.
    # local: don't run in sandbox or on remote executor.
    # exclusive: run one test at a time, since they share a Bazel
    #   output directory. If we don't do this, tests must extract the bazel
    #   installation and start with a fresh cache every time, making them
    #   much slower.
    tags = kwargs.setdefault("tags", [])
    for t in ["e2e", "local", "exclusive"]:
        if t not in tags:
            tags.append(t)

    rules_msbuild_integration_test(
        name = name,
        workspace_files = srcs_name,
        bazel_binary = "@build_bazel_bazel_%s//:bazel_binary" % BAZEL_VERSION.replace(".", "_"),
        **kwargs
    )
