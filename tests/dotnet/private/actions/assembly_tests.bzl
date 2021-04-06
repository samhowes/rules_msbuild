"""Unit tests for assembly.bzl."""

load("@bazel_skylib//lib:unittest.bzl", "analysistest", "asserts")
load("@bazel_skylib//lib:dicts.bzl", "dicts")
load("//dotnet/private/actions:assembly.bzl", "process_deps")
load("//dotnet/private/rules:core.bzl", "ASSEMBLY_ATTRS")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo", "NuGetPackageInfo")

### testing support ###
_ProcessDepsInfo = provider(
    doc = "value for process_deps tests",
    fields = ["references", "packages", "copied_files"],
)

def _fake_file(name):
    return struct(
        basename = name,
    )

def _process_deps_fake_rule_impl(ctx):
    packages = [
        NuGetPackageInfo(
            name = name,
            version = version,
            frameworks = {},
        )
        for name, version in ctx.attr.packages.items()
    ]

    deps = [
        {
            DotnetLibraryInfo: DotnetLibraryInfo(
                assembly = _fake_file(p.name),
                pdb = None,
                deps = depset(),
                package_info = p,
            ),
        }
        for p in packages
    ]

    references, packages, copied_files = process_deps(deps, "fake_tfm")
    return _ProcessDepsInfo(
        references = references,
        packages = packages,
        copied_files = copied_files,
    )

process_deps_fake_rule = rule(
    implementation = _process_deps_fake_rule_impl,
    attrs = dicts.add(ASSEMBLY_ATTRS, {
        "target_framework": attr.string(default = "fake_tfm"),
        "packages": attr.string_dict(),
        "fake_deps": attr.string_list(),
    }),
)

### tests
def _process_deps_works_test_impl(ctx):
    """Analysis test to make sure basic process_deps works."""
    env = analysistest.begin(ctx)

    target_under_test = analysistest.target_under_test(env)

    info = target_under_test[_ProcessDepsInfo]
    asserts.false(env, None == info.references)
    asserts.false(env, None == info.packages)
    asserts.false(env, None == info.copied_files)

    return analysistest.end(env)

process_deps_works_test = analysistest.make(_process_deps_works_test_impl)

def _process_deps_fails_fake_package_test_impl(ctx):
    """Analysis test to make sure basic process_deps works."""
    env = analysistest.begin(ctx)

    target_under_test = analysistest.target_under_test(env)

    asserts.expect_failure(env, "was not fetched")

    return analysistest.end(env)

process_deps_fails_fake_package_test = analysistest.make(_process_deps_fails_fake_package_test_impl, expect_failure = True)

# buildifier: disable=unnamed-macro
def assembly_test_suite():
    """Creates the test targets and test suite for restore.bzl tests."""

    process_deps_works_test(
        name = "process_deps_works_test",
        target_under_test = ":process_deps_works_fake_target",
    )
    process_deps_fake_rule(
        name = "process_deps_works_fake_target",
        tags = ["manual"],
    )

    process_deps_fails_fake_package_test(
        name = "process_deps_fails_fake_package_test",
        target_under_test = ":process_deps_fails_fake_package_fake_target",
    )
    process_deps_fake_rule(
        name = "process_deps_fails_fake_package_fake_target",
        tags = ["manual"],
        packages = {
            "fake_package": "0.0.0",
        },
    )
