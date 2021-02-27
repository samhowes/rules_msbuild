# Dotnet Toolchains

The dotnet toolchain is used to customize the behavior of the [core](./core.md) dotnet rules

Contents

- [Overview](#overview)
  - [The SDK](#the_sdk)
  - [The Toolchain](#the_toolchain)
  - [The Context](#the_context)
- [Rules and Functions](#rules_and_functions)

# Overview

The dotnet toolchain consists of three main layers: the SDK, the toolchain, and the context.

## The SDK

The Dotnet sdk is a directory tree containing the binaries for the Dotnet toolchain. It mostly consists of the binaries downloaded from [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download) configured for bazel with some starlark .bazel files.

You can configure the Dotnet SDK yourself with:

- [dotnet_download_sdk](#dotnet_download_sdk)

If an sdk has not already been configured, the dotnet_register_toolchains function creates a repository named @dotnet_sdk using dotnet_download_sdk, using a recent version of Dotnet for the host operating system and architecture.

## The Toolchain

The workspace rules above declare Bazel toolchains with dotnet_toolchain implementations for each target platform that Dotnet supports. Wrappers around the rules register these toolchains automatically. Bazel will select a registered toolchain automatically based on the execution and target platforms, specified with --host_platform and --platforms, respectively.

The toolchain itself should be considered opaque. You should only access its contents through the context.

## The Context

todo: learn about this

# Rules and Functions

## dotnet_register_toolchains

Installs the Dotnet toolchains. If `version` is specified, sets the SDK Version to use. (for example 3.1).

## dotnet_download_sdk

This downloads a Dotnet sdk for use in toolchains
