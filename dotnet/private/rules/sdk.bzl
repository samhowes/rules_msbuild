"""Defines the actual SDK after it has been downloaded"""

load(
    "//dotnet/private:providers.bzl",
    "DotnetConfigInfo",
    "DotnetSdkInfo",
    "TfmMappingInfo",
)

def _dotnet_sdk_impl(ctx):
    return [DotnetSdkInfo(
        dotnet = ctx.executable.dotnet,
        dotnetos = ctx.attr.dotnetos,
        dotnetarch = ctx.attr.dotnetarch,
        root_file = ctx.file.root_file,
        sdk_root = ctx.file.sdk_root,
        bazel_props = ctx.file.bazel_props,
        major_version = ctx.attr.major_version,
        runfiles = depset([
            ctx.file.bazel_props,
        ]),
        config = ctx.attr.config[DotnetConfigInfo],
    )]

def _dotnet_config_impl(ctx):
    return DotnetConfigInfo(
        nuget_config = ctx.file.nuget_config,
        trim_path = ctx.attr.trim_path,
        tfm_mapping = ctx.attr.tfm_mapping[TfmMappingInfo].dict,
        test_logger = ctx.attr.test_logger,
    )

dotnet_sdk = rule(
    _dotnet_sdk_impl,
    doc = ("Collects information about a Dotnet SDK. The SDK must have a normal " +
           "dotnet sdk directory structure."),
    provides = [DotnetSdkInfo],
    attrs = {
        "bazel_props": attr.label(
            mandatory = True,
            allow_single_file = True,
        ),
        "config": attr.label(
            mandatory = True,
            providers = [DotnetConfigInfo],
            doc = "The dotnet_config object for this sdk.",
        ),
        "dotnet": attr.label(
            mandatory = True,
            allow_single_file = True,
            executable = True,
            cfg = "exec",
            doc = "The dotnet binary",
        ),
        "dotnetarch": attr.string(
            mandatory = True,
            doc = "The host architecture the SDK was built for",
        ),
        "dotnetos": attr.string(
            mandatory = True,
            doc = "The host OS the SDK was built for",
        ),
        "fxr": attr.label(
            mandatory = True,
            allow_files = True,
            doc = ("The hstfxr.dll"),
        ),
        "home_dir": attr.label(
            mandatory = True,
            allow_single_file = True,
            doc = """The dotnet home directory, we place some sentinel files here to prevent
            dotnet from writing out its welcome message""",
        ),
        "major_version": attr.int(mandatory = True),
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
        "packs": attr.label(
            mandatory = True,
            allow_single_file = True,
        ),
        "shared": attr.label(
            mandatory = True,
            allow_single_file = True,
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
        "test_logger": attr.label(
            mandatory = True,
            doc = "Bazel compatible test logger nuget package",
        ),
    },
)
