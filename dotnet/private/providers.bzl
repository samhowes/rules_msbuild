"""Dotnet Providers"""

# See dotnet/providers.md#DotnetLibraryInfo for full documentation.
DotnetLibraryInfo = provider(
    doc = "Contains information about a Dotnet library",
    fields = {
        "assembly": "The primary assembly that was compiled",
        "pdb": "The pdb debug information, if available",
        "deps": "A depset of info structs for this library's dependencies",
    },
)

# See dotnet/providers.md#DotnetContextInfo for full documentation.
DotnetContextInfo = provider(
    doc = "A dotnet context",
    fields = {},
)

DotnetSdkInfo = provider(
    doc = "Contains information about the Dotnet SDK used in the toolchain",
    fields = {
        "dotnetos": "The host OS the SDK was built for.",
        "dotnetarch": "The host architecture the SDK was built for.",
        "root_file": "A file in the SDK root directory",
        "nuget_build_config": "Build-time nuget.config, should not have network packages. ",
        "sdk_root": ("The versioned root (typically in Sdk/<{version}>/ of the " +
                     "extracted folder"),
        "sdk_files": ("The files under sdk_root"),
        "fxr": ("The hstfxr.dll"),
        "shared": ("The shared sdk libraries"),
        "packs": ("NuGet packages included with the SDK"),
        "tools": ("List of executable files in the SDK built for " +
                  "the execution platform, excluding the dotnet binary file"),
        "dotnet": "The dotnet binary file",
    },
)
