"""Unit tests for restore.bzl."""

load("@bazel_skylib//lib:unittest.bzl", "analysistest", "asserts")
load("@bazel_skylib//lib:dicts.bzl", "dicts")
load("//dotnet/private/actions:restore.bzl", "restore")
load("//dotnet/private/rules:core.bzl", "ASSEMBLY_ATTRS")
load("//dotnet/private:providers.bzl", "DotnetSdkInfo", "NuGetPackageInfo")

### testing support ###
_RestoreTestInfo = provider(
    doc = "value for restore tests",
    fields = ["restore_file", "outputs"],
)

def _restore_fake_rule_impl(ctx):
    fake_sdk = DotnetSdkInfo(
        root_file = struct(dirname = "fake_sdk"),
        init_files = [],
        sdk_files = [],
        nuget_build_config = ctx.file._nuget_config,
        dotnetos = "windows",
        dotnet = "fakedotnet",
    )

    packages = [
        NuGetPackageInfo(
            name = name,
            version = version,
            is_fake = True,
        )
        for name, version in ctx.attr.packages.items()
    ]
    restore_file, outputs, cmd_outputs = restore(ctx, fake_sdk, ctx.attr.name + "/intermediate_path", packages)
    return _RestoreTestInfo(restore_file = restore_file, outputs = outputs)

restore_fake_rule = rule(
    implementation = _restore_fake_rule_impl,
    attrs = dicts.add(ASSEMBLY_ATTRS, {
        "target_framework": attr.string(default = "fake_tfm"),
        "packages": attr.string_dict(),
        "_nuget_config": attr.label(allow_single_file = True, default = ":nuget_build.config"),
    }),
)

### tests
def _restore_works_test_impl(ctx):
    """Analysis test to make sure basic restore works."""
    env = analysistest.begin(ctx)

    target_under_test = analysistest.target_under_test(env)

    info = target_under_test[_RestoreTestInfo]
    asserts.false(env, None == info.restore_file)
    asserts.false(env, None == info.outputs)

    return analysistest.end(env)

restore_works_test = analysistest.make(_restore_works_test_impl)

# buildifier: disable=unnamed-macro
def restore_test_suite():
    """Creates the test targets and test suite for restore.bzl tests."""

    restore_works_test(
        name = "restore_works_test",
        target_under_test = ":restore_works_fake_target",
    )
    restore_fake_rule(
        name = "restore_works_fake_target",
        tags = ["manual"],
    )
