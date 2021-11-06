# Understanding the build

The target audience of this document is People who:

1. Know nothing more than `dotnet build`
2. Are familiar with MSBuild project files but new to Bazel
3. Are familiar with Bazel but new to MSBuild

> Note: `dotnet build` and most other dotnet cli commands simply
> [delegate execution to msbuild](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-build#msbuild).
> These rules aim to achieve feature parity with `dotnet build myproject.csproj` but deal with
> msbuild functionality to build.

Given the setup:

```bash
dotnet new console -o console
dotnet tool install -g samhowes.bzl
bzl init
bazl run //:gazelle
bazel run //console     # => Hello World!
```

## Step 0: Build file generation

Bazel operates using BUILD[.bazel] files. At this time, bazel cannot comprehend files other than
`.bzl` or `.bazel` files to describe its build in the
[Starlark language](https://docs.bazel.build/versions/main/skylark/language.html). MSBuild on the
other hand orchestrates its build through project files written in XML. The primary role of
rules_msbuild is translating between these two orchestration languages.

> Note: MSBuild comprehends XML with several extensions, and some files have specific names with
> special meaning. A valid MSBuild project file is an xml file with a `<Project>` root element. The
> primary content of a project file is `<ItemGroup>` and `<PropertyGroup>` elements.

A goal of rules_msbuild is that the user need not know much about bazel in order to use and maintain
a repository of dotnet code that uses bazel to build. To this end, the dotnet cli tool
[SamHowes.Bzl](../dotnet/tools/Bzl/Readme.md) initializes your rules_msbuild workspace with the
right macro invocations as well as initializing some `Build.props` files so that your IDE will use
bazel instead of the normal build targets.

`bzl init` also generates a bootstrapping target: `//:gazelle`.
[Gazelle-dotnet](../gazelle/dotnet/Readme.md) parses your MSBuild project files and generates the
appropriate Starlark to orchestrate your build with bazel.

## Step 1: Loading Phase

In bazel's loading phase, bazel will find the "rules_msbuild" http_archive
