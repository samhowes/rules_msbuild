load(
    "//dotnet/private/rules:msbuild.bzl",
    _msbuild_binary = "msbuild_binary",
    _msbuild_library = "msbuild_library",
    _msbuild_test = "msbuild_test",
)

msbuild_binary = _msbuild_binary
msbuild_library = _msbuild_library
msbuild_test = _msbuild_test
