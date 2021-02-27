load("//dotnet/private:providers.bzl", "DotnetLibraryInfo")
load("//dotnet/private:context.bzl", "dotnet_context")


def dotnet_tool_binary(**kwargs):
    """builds binaries that only depend on the std library, for tools inside the toolchain"""
    print("dotnet_tool_binary")


def _dotnet_binary_impl(ctx):
    """dotnet_binary_impl emits actions for compiling dotnet code"""
    dotnet = dotnet_context(ctx)

    executable, outputs = dotnet.binary(dotnet)
    return [
        DefaultInfo(
            files = depset([executable]),
            # runfiles = dotnet._ctx.runfiles(files=[proj]),
            runfiles = None,
            executable = executable,
        ),
    ]

_dotnet_binary_kwargs = {
    "implementation": _dotnet_binary_impl,
    "attrs": {
        "srcs": attr.label_list(allow_files = [".cs"]),
        "target_framework": attr.string(
            mandatory=True,
            doc=("Target Framework Monikor (TFM) for the target .NET Framework i.e. netcoreapp3.1" + 
            " https://docs.microsoft.com/en-us/dotnet/standard/frameworks")
        ),
        "data": attr.label_list(allow_files = True),
        "deps": attr.label_list(
            providers = [DotnetLibraryInfo],
        ),
        "_proj_template": attr.label(
            default = Label("//dotnet/private/rules:compile.tpl.proj"),
            allow_single_file = True,
        ),
        "_dotnet_context_data": attr.label(default = "//:dotnet_context_data")
    },
    "executable": True,
    "toolchains": ["@my_rules_dotnet//dotnet:toolchain"],
}

dotnet_binary = rule(**_dotnet_binary_kwargs)
    