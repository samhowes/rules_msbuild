load("@bazel_skylib//lib:paths.bzl", "paths")
# NuGet.Client\src\NuGet.Core\NuGet.Common\PathUtil\NuGetEnvironment.cs

# Unused NuGetFolderPaths:
# DefaultMsBuildPath
## Derived NuGetFolderPaths:
# MachineWideConfigDirectory: MachineWideSettingsBaseDirectory\Config: C:\Program Files\NuGet\Config

NugetEnvironmentInfo = provider(
    doc = """A provider representing NuGet's SpecialFolders Enum as found in NuGet.Client/src/NuGet.Core/NuGet.Common/PathUtil/NuGetEnvironment.cs
Doc string format: EnvironmentVariable(Windows|Mac|Linux) => ExamplePath(Windows|Mac|Linux)
""",
    fields = {
        "UserProfile": "(~) USERPROFILE|HOME|HOME => C:\\Users\\sam|/Users/sam|/home/sam",
        # NuGetHome: \.nuget
        "ApplicationData": "APPDATA|???|XDG_CONFIG_HOME => ~\\AppData\\Roaming|???|~/.config",
        # UserSettingsDirectory: /NuGet
        "LocalApplicationData": "LOCALAPPDATA|???|XDG_DATA_HOME => ~\\AppData\\Local|???|~/.local/share",
        # HttpCacheDirectory: \NuGet\v3-cache
        # NuGetPluginsCacheDirectory: \NuGet\plugins-cache

        # IsWindows
        "ProgramFilesX86": "Windows Only: ProgramFiles(x86) => \"C:\\Program Files (x86)\"",
        "ProgramFiles": "Windows Only: (Used when X86 is not available) PROGRAMFILES => \"C:\\Program Files\"",
        # MachineWideSettingsBaseDirectory: \NuGet
        # Else
        "CommonApplicationData": "non-windows only: ???|null => ???|/usr/share",
        # MachineWideSettingsBaseDirectory: /NuGet
    },
)

# https://developers.redhat.com/blog/2018/11/07/dotnet-special-folder-api-linux/
# a mapping of fields of NugetEnvironmentInfo to environment variable names on linux XDG systems
XDG_ENVIRONMENT = dict(
    UserProfile = "HOME",
    ApplicationData = "XDG_CONFIG_HOME",
    LocalApplicationData = "XDG_DATA_HOME",
    ProgramFilesX86 = "",
    ProgramFiles = "",
    CommonApplicationData = "",
)

# a mapping of fields of NugetEnvironmentInfo to environment variable names on windows
WIN_ENVIRONMENT = dict(
    UserProfile = "USERPROFILE",
    ApplicationData = "APPDATA",
    LocalApplicationData = "LOCALAPPDATA",
    ProgramFilesX86 = "ProgramFiles(x86)",
    ProgramFiles = "PROGRAMFILES",
    CommonApplicationData = "",
)

# a mapping of fields of NugetEnvironmentInfo to environment variable names on macos
DARWIN_ENVIRONMENT = dict(
    UserProfile = "HOME",
    ApplicationData = "???",
    LocalApplicationData = "???",
    ProgramFilesX86 = "",
    ProgramFiles = "",
    CommonApplicationData = "???",
)

NUGET_ENVIRONMENTS = {
    "windows": WIN_ENVIRONMENT,
    "linux": XDG_ENVIRONMENT,
    "darwin": DARWIN_ENVIRONMENT,
}

def isolated_environment(repo_root):
    isolated_kwargs = {}
    for k, _ in WIN_ENVIRONMENT.items():
        isolated_kwargs[k] = paths.join(repo_root, "nuget")

    return NugetEnvironmentInfo(**isolated_kwargs)
