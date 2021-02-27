# todo: https://github.com/bazelbuild/rules_go/blob/master/go/private/rules/binary.bzl

# dotnet_tool_binary: builds binaries that only depend on the std library, for tools inside the toolchain

def dotnet_tool_binary():
    print("dotnet_tool_binary")