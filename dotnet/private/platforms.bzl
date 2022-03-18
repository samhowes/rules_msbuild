"""platforms.bzl defines PLATFORMS, a table that describes each possible
target platform. This table is used to generate config_settings,
constraint_values, platforms, and toolchains."""

BAZEL_DOTNETOS_CONSTRAINTS = {
    "darwin": "@platforms//os:osx",
    "linux": "@platforms//os:linux",
    "windows": "@platforms//os:windows",
}

BAZEL_DOTNETARCH_CONSTRAINTS = {
    "amd64": "@platforms//cpu:x86_64",
    "arm64": "@platforms//cpu:aarch64",
}

DOTNETOS_DOTNETARCH = (
    ("darwin", "arm64"),
    ("darwin", "386"),
    ("darwin", "amd64"),
    ("linux", "amd64"),
    ("windows", "amd64"),
)

def _generate_constraints(names, bazel_constraints):
    return {
        name: bazel_constraints.get(name, "@rules_msbuild//dotnet:" + name)
        for name in names
    }

DOTNETOS_CONSTRAINTS = _generate_constraints([p[0] for p in DOTNETOS_DOTNETARCH], BAZEL_DOTNETOS_CONSTRAINTS)
DOTNETARCH_CONSTRAINTS = _generate_constraints([p[1] for p in DOTNETOS_DOTNETARCH], BAZEL_DOTNETARCH_CONSTRAINTS)

def _generate_platforms():
    platforms = []
    for dotnetos, dotnetarch in DOTNETOS_DOTNETARCH:
        constraints = [
            DOTNETOS_CONSTRAINTS[dotnetos],
            DOTNETARCH_CONSTRAINTS[dotnetarch],
        ]
        platforms.append(struct(
            name = dotnetos + "_" + dotnetarch,
            dotnetos = dotnetos,
            dotnetarch = dotnetarch,
            constraints = constraints,
        ))
    return platforms

PLATFORMS = _generate_platforms()

def generate_toolchain_names():
    # keep in sync with declare_toolchains
    return ["dotnet_" + p.name for p in PLATFORMS]
