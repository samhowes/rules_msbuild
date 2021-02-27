"""Defines the actual SDK after it has been downloaded"""
load(
    "//dotnet/private:providers.bzl",
    "DotnetSdkInfo",
)

def _dotnet_sdk_impl(ctx):
    return [DotnetSdkInfo(
        dotnetos = ctx.attr.dotnetos,
        dotnetarch = ctx.attr.dotnetarch,
        root_file = ctx.file.root_file,
        libs = ctx.files.libs,
        dotnet = ctx.executable.dotnet,
    )]

dotnet_sdk = rule(
    _dotnet_sdk_impl,
    attrs = {
        "dotnetos": attr.string(
            mandatory = True,
            doc = "The host OS the SDK was built for",
        ),
        "dotnetarch": attr.string(
            mandatory = True,
            doc = "The host architecture the SDK was built for",
        ),
        "root_file": attr.label(
            mandatory = True,
            allow_single_file = True,
            doc = "A file in the SDK root directory.",
        ),
        "libs": attr.label_list(
            allow_files = [".a"],
            doc = ("Pre-compiled .dll files for the standard library, " +
                   "built for the execution platform"),
        ),
        "tools": attr.label_list(
            allow_files = True,
            cfg = "exec",
            doc = ("List of executable files in the SDK built for " +
                   "the execution platform, excluding the go binary"),
        ),
        "dotnet": attr.label(
            mandatory = True,
            allow_single_file = True,
            executable = True,
            cfg = "exec",
            doc = "The dotnet binary",
        ),
    },
    doc = ("Collects information about a Dotnet SDK. The SDK must have a normal " +
           "dotnet sdk directory structure."),
    provides = [DotnetSdkInfo],
)

def dotnet_tool_binary(**kwargs):
    print("dotnet_tool_binary")

def package_list(**kwargs):
    print("package_list")
