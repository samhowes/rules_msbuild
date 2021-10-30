load(
    "//dotnet/private/rules:nuget_download.bzl",
    _framework_info = "framework_info",
    _nuget_package_framework_version = "nuget_package_framework_version",
    _nuget_package_version = "nuget_package_version",
    _tfm_mapping = "tfm_mapping",
)
load(
    "//dotnet/private:nuget_macros.bzl",
    _nuget_package_download = "nuget_package_download",
)

nuget_package_download = _nuget_package_download
framework_info = _framework_info
nuget_package_framework_version = _nuget_package_framework_version
nuget_package_version = _nuget_package_version
tfm_mapping = _tfm_mapping
