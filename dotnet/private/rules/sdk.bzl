"""Defines the actual SDK after it has been downloaded"""

load(
    "//dotnet/private:providers.bzl",
    "DotnetConfigInfo",
    "DotnetSdkInfo",
    "TfmMappingInfo",
)

def _dotnet_sdk_impl(ctx):
    shared_dict = {}
    for shared in ctx.attr.shared:
        shared_dict[shared.label.name] = shared[DefaultInfo].files

    return [DotnetSdkInfo(
        dotnet = ctx.executable.dotnet,
        dotnetos = ctx.attr.dotnetos,
        dotnetarch = ctx.attr.dotnetarch,
        root_file = ctx.file.root_file,
        sdk_root = ctx.file.sdk_root,
        sdk_files = ctx.files.sdk_files,
        fxr = ctx.files.fxr,
        shared = shared_dict,
        packs = depset(ctx.files.packs),
        init_files = depset(ctx.files.init_files),
        all_files = depset(ctx.files.all_files),
        config = ctx.attr.config[DotnetConfigInfo],
    )]

def _dotnet_config_impl(ctx):
    return DotnetConfigInfo(
        nuget_config = ctx.file.nuget_config,
        trim_path = ctx.attr.trim_path,
        tfm_mapping = ctx.attr.tfm_mapping[TfmMappingInfo].dict,
    )

dotnet_sdk = rule(
    _dotnet_sdk_impl,
    doc = ("Collects information about a Dotnet SDK. The SDK must have a normal " +
           "dotnet sdk directory structure."),
    provides = [DotnetSdkInfo],
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
        "init_files": attr.label(
            mandatory = True,
            allow_files = True,
            doc = "The dotnet init files, these prevent noisy welcome messages on first build",
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
        "all_files": attr.label(
            mandatory = True,
            doc = ("All files that comprise the sdk."),
        ),
        "fxr": attr.label(
            mandatory = True,
            allow_files = True,
            doc = ("The hstfxr.dll"),
        ),
        "shared": attr.label_list(
            mandatory = True,
            doc = "The shared sdk libraries",
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
        "config": attr.label(
            mandatory = True,
            providers = [DotnetConfigInfo],
            doc = "The dotnet_config object for this sdk.",
        ),
    },
)

dotnet_config = rule(
    _dotnet_config_impl,
    doc = ("Collects configuration information to build a dotnet assembly."),
    provides = [DotnetConfigInfo],
    attrs = {
        "nuget_config": attr.label(
            mandatory = True,
            allow_single_file = True,
            doc = "Build-time nuget.config, configures nuget to not fetch any packages on the internet.",
        ),
        "trim_path": attr.string(
            mandatory = True,
            doc = "Used by the builder, a path to trim from msbuild outputs.",
        ),
        "tfm_mapping": attr.label(
            mandatory = True,
            doc = "Used to locate runtime files in the dotnet sdk.",
        ),
    },
)
