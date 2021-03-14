"""Defines the nuget repository rules"""

load("@bazel_skylib//lib:paths.bzl", "paths")

DEFAULT_REPOSITORY_NAME = "nuget"
_BOOTSTRAP_FILE_NAME = "BOOTSTRAP"
_FAKE_INIT = "fake_init=true"
_BOOTSTRAP_WARNING = ("@{repo_name} did not download any nuget packages, run @{repo_name}//:bootstrap" +
                      "to bootstrap a package restore. See <insert docs link here> for more information on bootstrapping.")

def nuget_config(
        name = DEFAULT_REPOSITORY_NAME,
        deps = [],
        package_specs = []):
    """Attempts a nuget restore, fails with instructions if Bootstrapping is necessary

    Args:
        name: (default="nuget") the repository name that packages will be made available under
        deps: a generated list of targets from bazel query
        package_specs: a list of "PackageName:version" strings i.e. ["NewtonSoft.Json:12.0.3","CommandLineParser:2.8.0"]
    """

    _nuget_config(
        name = name,
        package_specs = package_specs,
    )

def _nuget_config_impl(ctx):
    packages = _parse_specs(ctx.attr.package_specs)
    for package in packages:
        ctx.template(
            paths.join(package.dirname, "BUILD.bazel"),
            Label("@my_rules_dotnet//dotnet/private/nuget:BUILD.package.tpl.bazel"),
            substitutions = {
                "{repo_name}": ctx.name,
                "{name}": package.name,
                "{version}": package.version,
            },
        )
    ctx.file("ROOT")
    ctx.template(
        "BUILD",
        Label("@my_rules_dotnet//dotnet/private/nuget:BUILD.tpl.bazel"),
        substitutions = {},
    )
    print(_BOOTSTRAP_WARNING.format(repo_name = ctx.name))

def _parse_specs(package_specs):
    """Parses packages specs into a list of struct(name,dirnameF,version,spec)"""
    packages = []
    for ps in package_specs:
        parts = ps.split(":")
        if (len(parts) != 2):
            fail('package_spec must be a string of the format "PackageName:version". Got: "{}"'.format(ps))

        packages.append(
            struct(
                name = parts[0],
                dirname = parts[0].lower(),
                version = parts[1],
                spec = ps,
            ),
        )
    return packages

def _nuget_environment(ctx):
    pass

_nuget_config = repository_rule(
    implementation = _nuget_config_impl,
    attrs = {
        "package_specs": attr.string_list(),
    },
)
