"""Defines the nuget repository rules"""

load("@bazel_skylib//lib:paths.bzl", "paths")

DEFAULT_REPOSITORY_NAME = "nuget"
_BOOTSTRAP_FILE_NAME = "BOOTSTRAP"
_FAKE_INIT = "fake_init=true"
_BOOTSTRAP_WARNING = ("@{repo_name} did not download any nuget packages, run @{repo_name}//:bootstrap" +
                      "to bootstrap a package restore. See <insert docs link here> for more information on bootstrapping.")

def nuget_restore(
        name = DEFAULT_REPOSITORY_NAME,
        package_specs = []):
    """Attempts a nuget restore, fails with instructions if Bootstrapping is necessary
    
    Args:
        name: (default="nuget") the repository name that packages will be made available under
        package_specs: a list of "PackageName:version" strings i.e. ["NewtonSoft.Json:12.0.3","CommandLineParser:2.8.0"]
    """

    _nuget_restore(
        name = name,
        package_specs = package_specs,
    )

def _nuget_restore_impl(ctx):
    if ctx.path(_BOOTSTRAP_FILE_NAME).exists:
        print("Found bootstrap file.")
        print(_BOOTSTRAP_WARNING.format(repo_name = ctx.name))
        return

    packages = _parse_specs(ctx.attr.package_specs)
    for package in packages:
        ctx.template(
            paths.join(package.dirname, "BUILD.bazel"),
            Label("@my_rules_dotnet//dotnet/private/nuget:BUILD.fake.bazel"),
            substitutions = {
                "{repo_name}": ctx.name,
                "{name}": package.name,
            },
        )
    ctx.file(_BOOTSTRAP_FILE_NAME, _FAKE_INIT)
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

_nuget_restore = repository_rule(
    implementation = _nuget_restore_impl,
    attrs = {
        "package_specs": attr.string_list(),
    },
)
