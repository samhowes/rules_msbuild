# Dotnet Rules for Bazel

<!--
Links
 -->

My attempt at re-implementing bazelbuild/rules_dotnet with the primary goal of reducing checked-in code generation.

Inspiration taken from [rules_dotnet](https://github.com/bazelbuild/rules_dotnet) and [rules_go](https://github.com/bazelbuild/rules_go).

# Contents

- [Overview](#overview)
- [Setup](#setup)

# Overview

bazelbuild/rules_dotnet has many checked in files that reference assemblies directly. I'm tyring to figure out if those are actually necessary by re-implementing from scratch. Much of these docs, and this implementation, is targeted at developers coming from msbuild, Visual Studio, and Windows. It should be cross platform though.

# Setup

## System Setup

Unkown

## Initial Project Setup

Create a file at the top of your repository named WORKSPACE, and add the snippet below (or add to your existing WORKSPACE). This tells Bazel to fetch rules_dotnet and its dependencies. Bazel will download a recent supported Dotnet toolchain and register it for use.

```python
# todo: correct this
load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")

http_archive(
    name = "my_rules_dotnet",
    sha256 = "7904dbecbaffd068651916dce77ff3437679f9d20e1a7956bff43826e7645fcc",
    urls = [
        "https://mirror.bazel.build/github.com/bazelbuild/rules_go/releases/download/v0.25.1/rules_go-v0.25.1.tar.gz",
        "https://github.com/bazelbuild/rules_go/releases/download/v0.25.1/rules_go-v0.25.1.tar.gz",
    ],
)

load("@my_rules_dotnet//dotnet:deps.bzl", "dotnet_register_toolchains", "dotnet_rules_dependencies")

dotnet_rules_dependencies()

dotnet_register_toolchains(version = "1.16")
```

Add a file named BUILD(.bazel) where you would normally put your .csproj file.

```python
load("@my_rules_dotnet//dotnet:def.bzl", "dotnet_binary")

dotnet_binary(
    name = "hello",
    srcs = ["Program.cs"],
)
```

You can build this target with bazel build //:hello.

## Generating build files

This is a stretch goal at the moment.
