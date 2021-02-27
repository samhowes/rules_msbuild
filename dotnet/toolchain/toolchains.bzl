load(
    "@my_rules_dotnet//dotnet/private:platforms.bzl",
    "DOTNETARCH_CONSTRAINTS",
    "DOTNETOS_CONSTRAINTS",
    "PLATFORMS",
)

def declare_constraints():
    """Generates constraint_values and platform targets for valid platforms.

    Each constraint_value corresponds to a valid dotnetos or dotnetarch.
    The dotnetos and dotnetarch values belong to the constraint_settings
    @platforms//os:os and @platforms//cpu:cpu, respectively.
    To avoid redundancy, if there is an equivalent value in @platforms,
    we define an alias here instead of another constraint_value.

    Each platform defined here selects a dotnetos and dotnetarch constraint value.
    These platforms may be used with --platforms for cross-compilation,
    though users may create their own platforms (and
    @bazel_tools//platforms:default_platform will be used most of the time).
    """
    for dotnetos, constraint in DOTNETOS_CONSTRAINTS.items():
        if constraint.startswith("@my_rules_dotnet//dotnet/toolchain:"):
            native.constraint_value(
                name = dotnetos,
                constraint_setting = "@platforms//os:os",
            )
        else:
            native.alias(
                name = dotnetos,
                actual = constraint,
            )

    for dotnetarch, constraint in DOTNETARCH_CONSTRAINTS.items():
        if constraint.startswith("@my_rules_dotnet//dotnet/toolchain:"):
            native.constraint_value(
                name = dotnetarch,
                constraint_setting = "@platforms//cpu:cpu",
            )
        else:
            native.alias(
                name = dotnetarch,
                actual = constraint,
            )

    for p in PLATFORMS:
        native.platform(
            name = p.name,
            constraint_values = p.constraints,
        )
