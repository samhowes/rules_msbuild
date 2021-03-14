"""Actions for preparing a dotnet restore"""

load("//dotnet/private/actions:xml.bzl", "inline_element")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo")

def pre_restore(ctx):
    """Emits an action for generating files necessary for a nuget restore
    
    Args:
        ctx: the ctx of the nuget_pre_restore rule
    Returns:
        The generated .props file
    """
    packages = [
        p[DotnetLibraryInfo]
        for p in ctx.attr.deps
        if hasattr(p[DotnetLibraryInfo], "package_info")
    ]

    props_file = _props_file(ctx, packages)

    return props_file

def _props_file(ctx, packages):
    tfm = ctx.attr.target_framework
    primary = ctx.attr.primary_name

    props_file = ctx.actions.declare_file(primary + ".nuget.props")

    package_references = [
        inline_element(
            "PackageReference",
            {
                "Include": p.package_info.name,
                "Version": p.package_info.version,
            },
        )
        for p in packages
    ]

    ctx.actions.expand_template(
        template = ctx.file._props_template,
        output = props_file,
        is_executable = False,
        substitutions = {
            "{tfm}": tfm,
            "{package_references}": "\n".join(package_references),
        },
    )
    return props_file, tfm
