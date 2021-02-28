"""Defines the actual SDK after it has been downloaded"""
load(
    "//dotnet/private:providers.bzl",
    "DotnetSdkInfo",
)

def _dotnet_sdk_impl(ctx):
    return [DotnetSdkInfo(
        dotnet = ctx.executable.dotnet,
        dotnetos = ctx.attr.dotnetos,
        dotnetarch = ctx.attr.dotnetarch,
        root_file = ctx.file.root_file,
        sdk_root = ctx.file.sdk_root,
        sdk_files = ctx.files.sdk_files,
        fxr = ctx.files.fxr,
        shared = ctx.files.shared,
        packs = ctx.files.packs,
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
        "sdk_root": attr.label(
            mandatory = True,
            allow_single_file = True,
            doc = ("The versioned directory containing the primary SDK" +
                     "Artifacts and build extensions"),
        ),
        "sdk_files": attr.label(
            mandatory = True,
            doc = ("The files inside the sdk_root"),
        ),
        "fxr": attr.label(
            mandatory = True,
            allow_files = True,
            doc = ("The hstfxr.dll"),
        ),
        "shared": attr.label(
            mandatory = True,
            allow_files = True,
            doc = "The shared sdk libraries"
        ),
        "packs": attr.label_list(
            mandatory = True,
            allow_files = True,
            doc = ("NuGet packages included with the SDK"),
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
