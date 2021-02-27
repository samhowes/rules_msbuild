"""Dotnet Providers"""

DotnetSdkInfo = provider(
    doc = "Contains information about the Dotnet SDK used in the toolchain",
    fields = {
        "dotnetos": "The host OS the SDK was built for.",
        "dotnetarch": "The host architecture the SDK was built for.",
        "root_file": "A file in the SDK root directory",
        "libs": ("List of pre-compiled .dll files for the standard library " +
                 "built for the execution platform."),
        "tools": ("List of executable files in the SDK built for " +
                  "the execution platform, excluding the dotnet binary file"),
        "dotnet": "The dotnet binary file",
    },
)
