load("//dotnet/private/rules:nuget_download.bzl", _nuget_package = "nuget_package_download")

def nuget_package_download(name = None, **kwargs):
    _nuget_package(name = name, **kwargs)

    # alias to the original package for simplicity in :msbuild.bzl
    # this allows macros in there to simply depend on the _restore targets of all its deps
    native.alias(name = name + "_restore", actual = name)
