"""Dotnet Providers"""

# See dotnet/providers.md#DotnetLibraryInfo for full documentation.
DotnetLibraryInfo = provider(
    doc = "Contains information about a Dotnet library",
    fields = {
        "assembly": "The primary assembly that was compiled",
        "pdb": "The pdb debug information, if available",
        "deps": "A depset of DotnetLibraryInfo for this library's dependencies",
        "package_info": "A NuGetPackageInfo provider if this is a nuget package.",
    },
)

# See dotnet/providers.md#DotnetContextInfo for full documentation.
DotnetContextInfo = provider(
    doc = "A dotnet context",
    fields = {},
)

NuGetPackageInfo = provider(
    doc = "Package restore information",
    fields = {
        "name": "Package name",
        "version": "A nuget version string",
        "is_fake": "A boolean indicating if this is a placeholder package for bootstrapping",
        "frameworks": (
            "A struct from a cannonical tfm (e.g. netcoreapp3.1) to NuGetFetchedPackageFrameworkInfo providers " +
            "for framework specific package information. The members of this struct are defined by the *package consumers*, not by " +
            "the package. i.e. if the package is a netstandard2.0 package, but the consuming target is targeting netcoreapp3.1, then " +
            "the key `netcoreapp3.1` will exist in this struct, *not* netstandard2.0. This matches up with the packages.lock.json and " +
            "allows package consumers to easily access the deps specific to their framework."
        ),
    },
)

def MSBuildSdk(name, version):
    """An msbuild sdk happens to actually be a NuGetPackage

    https://github.com/microsoft/MSBuildSdks#how-can-i-use-these-sdks
    """
    return NuGetPackageInfo(
        name = name,
        version = version,
    )

DEFAULT_SDK = MSBuildSdk("Microsoft.NET.Sdk", None)

NuGetFetchedPackageFrameworkInfo = provider(
    doc = "NuGetPackage info for a specific framework.",
    fields = {
        "tfm": "The canonical tfm that requested this package.",
        "assemblies": "depset of files used by consumers to compile.",
        "data": "List of files that are runtime dependencies of this package (these should be copied to the output directory).",
        "files": "List of *all* files that comprise this package for this framework. Includes .dll, .xml, other?",
        "deps": "Depset of NuGetPackageInfo that this package depends on for `tfm`.",
    },
)

DotnetSdkInfo = provider(
    doc = "Contains information about the Dotnet SDK used in the toolchain",
    fields = {
        "dotnetos": "The host OS the SDK was built for.",
        "dotnetarch": "The host architecture the SDK was built for.",
        "root_file": "A file in the SDK root directory",
        "init_files": "The init files for dotnet, these prevent dotnet from printing noisy welcome messages",
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
        "all_files": "all sdk files",
    },
)
