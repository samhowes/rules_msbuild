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
        "frameworks": (
            "A struct from a cannonical tfm (e.g. netcoreapp3.1) to NuGetFetchedPackageFrameworkInfo providers " +
            "for framework specific package information. The members of this struct are defined by the *package consumers*, not by " +
            "the package. i.e. if the package is a netstandard2.0 package, but the consuming target is targeting netcoreapp3.1, then " +
            "the key `netcoreapp3.1` will exist in this struct, *not* netstandard2.0. This matches up with the packages.lock.json and " +
            "allows package consumers to easily access the deps specific to their framework."
        ),
        "all_files": "depset of All the files that comprise the nuget package. NuGet enumerates these for us each time " +
                     "it does a restore.",
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

NuGetFilegroupInfo = provider(
    doc = "A group of files for a NuGet package.",
    fields = {
        "name": "the name of this filegroup",
        "compile": "depset of files used by consumers to compile.",
        "runtime": "depset of files to be copied to the output directory by MsBuild.",
    },
)

DotnetSdkInfo = provider(
    doc = "Contains information about the Dotnet SDK used in the toolchain",
    fields = {
        "dotnetos": "The host OS the SDK was built for.",
        "dotnetarch": "The host architecture the SDK was built for.",
        "root_file": "A file in the SDK root directory",
        "init_files": "The init files for dotnet, these prevent dotnet from printing noisy welcome messages",
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
        "config": "the dotnet_config.",
    },
)

DotnetConfigInfo = provider(
    doc = "Provider for dotnet_config",
    fields = {
        "nuget_config": "Build-time nuget.config, configures nuget to not fetch any packages on the internet.",
        "trim_path": "path for the builder to trim to bazelify and unbazelify inputs and outputs of msbuild.",
    },
)
