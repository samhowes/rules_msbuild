load("//dotnet/private/rules:nuget.bzl", _nuget_package = "nuget_package")

def nuget_package(name = None, **kwargs):
    _nuget_package(name = name, **kwargs)

    # alias to the original package for simplicity in :msbuild.bzl
    # this allows macros in there to simply depend on the _restore targets of all its deps
    native.alias(name = name + "_restore", actual = name)
