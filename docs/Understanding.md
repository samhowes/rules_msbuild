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

todo(#15): show tree of current directory structure

## Step 1: Loading Phase

### rules_msbuild and dotnet_sdk instantiation

In bazel's loading phase, bazel will find the `http_archive(name="rules_msbuild"...)` specified in
//:WORKSPACE and download the specified `rules_msubild<version>.tar.gz` from the specified
[release of rules_msbuild](https://github.com/samhowes/rules_msbuild/releases). This tarfile is
mostly the source code of rules*msbuild at a particular commit, but also contains some pre-built
binaries and nuget packages. The binaries don't \_need* to be prebuilt, but using the rules is more
convenient with the prebuilt items. In particular, gazelle-dotnet is prebuilt on linux, mac, and
windows so your workspace doesn't need to have a dependency on rules_go or bazel-gazelle.

Once the .tar.gz is downloaded, it is extracted to bazel-<your_workspace>/external/rules_msbuild.
Feel free to poke around the source code. Next, bazel will execute the
`msbuild_rules_dependencies()` macro to instantiate rules_msbuild's dependencies which are basically
just a couple [bazel utilities](../dotnet/deps.bzl).

Next, bazel will execute `msbuild_register_toolchains(use_host=True)`. This is the primary
instantiation of rules_msbuild. Since `version = "host"` was specified, rules_msbuild will query the
system for the highest installed dotnet sdk via `which dotnet` and `dotnet --list-sdks`.
rules_msbuild then creates symlinks to these files and folders in `external/dotnet_sdk` and then
generates BUILD files to inform bazel about how to use these files.

The "builder" is also copied into `external/dotnet_sdk`. The [builder](../dotnet/tools/builder) is
how rules_msbuild integrates with MSBuild using the
[Microsoft.Build](https://www.nuget.org/packages/Microsoft.Build/) nuget package. Currently, the
builder is compiled from sources in the users workspace and not distributed as a binary. So you'll
see some build output related to compiling it. More on the builder later.

> Note: version = "host" could be considered "not bazellike" because it uses items from the host
> machine, which is not hermetic. It is quite faster though, since a dotnet-sdk does not have to be
> downloaded fresh. For maximum hermeticity, pass `version="5.0.203"` and rules_msbuild will
> download dotnet sdk version 5.0.203 via the
> [dotnet-install scripts](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script).

### nuget instantiation

Even if you don't depend on any nuget packages, dotnet still needs to fetch some of its dependencies
from NuGet for `dotnet restore`, so gazelle generates a `nuget_fetch()` invocation for your
repository in a `nuget_deps` macro defined in `deps/nuget.bzl`. In the call to `nuget_fetch`, you'll
also see some packages loaded from
[`@rules_msbuild//deps/public.nuget.bzl`](../deps/public_nuget.bzl). These are packages that the
builder needs to run, as well as a bazel-compatible test logger package for writing JUnit xml test
logs.

If your call to `nuget_fetch` has `use_host=True`, rules_msbuild will use the dotnet cli to query
for your global packages folder with `dotnet nuget locals global-packages --list`, on all platforms
this folder is usually located at `~/.nuget/packages`. A symlink is then created to this directory
from `external/nuget/packages`. This allows bazel access to nuget packages on the host machine, so
bazel doesn't need to download these packages itself, this is how a normal `dotnet build` would
consume nuget packages and is the fastest way to instantiate rules_msbuild as it takes advantage of
your machine's package cache.

For maximum hermeticity, remove the parameter `use_host=True` and a new, empty folder will be
created at `external/nuget/packages` and all packages will be downloaded fresh.

> Note: use_host=True could be considered "not bazellike" because it uses items from the host
> machine, which is not hermetic. It can produce a quite faster startup time though since dotnet has
> a lot of dynamically fetched packages built in to the framework and not just for external
> dependencies.

`nuget_fetch` also copies the contents of
[`//dotnet/tools/NuGetParser`](../dotnet/tools/NuGetParser) into `external/nuget/fetch`.

todo(#15) complete this doc
