load("//dotnet/private:providers.bzl", "DotnetLibraryInfo")
load("//dotnet/private:context.bzl", "dotnet_context")


def dotnet_tool_binary(**kwargs):
    """builds binaries that only depend on the std library, for tools inside the toolchain"""
    print("dotnet_tool_binary")


def _dotnet_binary_impl(ctx):
    """dotnet_binary_impl emits actions for compiling dotnet code"""
    dotnet = dotnet_context(ctx)

    name = ctx.attr.basename
    if not name:
        name = ctx.label.name
    executable = None
    if ctx.attr.out:
        # Use declare_file instead of attr.output(). When users set output files
        # directly, Bazel warns them not to use the same name as the rule, which is
        # the common case with dotnet_binary.
        executable = ctx.actions.declare_file(ctx.attr.out)
    executable = dotnet.binary(
        dotnet,
        name = name,
        executable = executable,
    )
    return [
        DefaultInfo(
            files = depset([executable]),
            runfiles = None,
            executable = executable,
        ),
    ]

_dotnet_binary_kwargs = {
    "implementation": _dotnet_binary_impl,
    "attrs": {
        "srcs": attr.label_list(allow_files = [".cs"]),
        "data": attr.label_list(allow_files = True),
        "deps": attr.label_list(
            providers = [DotnetLibraryInfo],
        ),
        "basename": attr.string(),
        "out": attr.string(),
        "_dotnet_context_data": attr.label(default = "//:dotnet_context_data"),
    },
    "executable": True,
    "toolchains": ["@my_rules_dotnet//dotnet:toolchain"],
}

dotnet_binary = rule(**_dotnet_binary_kwargs)
    