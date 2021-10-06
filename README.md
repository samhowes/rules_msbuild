# Dotnet Rules for Bazel

| Windows                                                                                                                                                                                                                                                        | Mac                                                                                                                                                                                                                                                    | Linux                                                                                                                                                                                                                                                      |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [![Build Status](https://dev.azure.com/samhowes/rules_msbuild/_apis/build/status/samhowes.rules_msbuild?branchName=master&jobName=windows)](https://dev.azure.com/samhowes/rules_msbuild/_build/latest?definitionId=6&branchName=master&jobName=windows) | [![Build Status](https://dev.azure.com/samhowes/rules_msbuild/_apis/build/status/samhowes.rules_msbuild?branchName=master&jobName=mac)](https://dev.azure.com/samhowes/rules_msbuild/_build/latest?definitionId=6&branchName=master&jobName=mac) | [![Build Status](https://dev.azure.com/samhowes/rules_msbuild/_apis/build/status/samhowes.rules_msbuild?branchName=master&jobName=linux)](https://dev.azure.com/samhowes/rules_msbuild/_build/latest?definitionId=6&branchName=master&jobName=linux) |

<!--
Links
 -->
> These docs are under construction. Please open an issue for any specific questions!

# In Beta!
```bash
# set up a hello world dotnet app
mkdir -p HelloBazelDotnet/AwesomeExecutable && cd HelloBazelDotnet/AwesomeExecutable
dotnet new console --no-restore
cd .. && dotnet new sln && dotnet sln add AwesomeExecutable
 
dotnet tool install -g SamHowes.Bzl
samhowes.bzl                     # automatically configure your workspace
bazel run //:gazelle             # generate build files with custom Gazelle language
bazel build //...                # use bazel to build .csproj files
bazel run //AwesomeExecutable    # => Hello World!
```

Check out the `tests/` directory & `e2e/` directory for examples

## Features
1. Build .csproj files with Bazel
1. `dotnet build` feature parity 
1. IDE Integration with JetBrains Rider / Visual Studio by default
1. Automated BUILD file generation
1. Runfiles Library
1. Bazel sandboxing compatible

### Blockers
1. Updating docs


# Usage

> Note: [SamHowes.Bzl](https://www.nuget.org/packages/SamHowes.Bzl/) and using gazelle (as described above) is strongly reccomended. 

```python
# //WORKSPACE
load("@bazel_tools//tools/build_defs/repo:git.bzl", "http_archive")

git_repository(
    name = "rules_msbuild",
    tag = "stable",
    remote = "https://github.com/samhowes/rules_msbuild"
)

load("@rules_msbuild//dotnet:py_deps.bzl", "dotnet_register_toolchains", "dotnet_rules_dependencies")

dotnet_rules_dependencies()
# See https://dotnet.microsoft.com/download/dotnet for valid versions
dotnet_register_toolchains(version = "3.1.100")
```

```python
# //hello/BUILD
load("@rules_msbuild//dotnet:def.bzl", "dotnet_binary")

dotnet_binary(
    name = "hello",
    srcs = ["Program.cs"],
)
```

`bazel build //hello`

# Background

This is a fresh implementation of bazel rules for dotnet that uses MSBuild to do the building. Since
MSBuild builds things, all .csproj features are fully supported.

## Challenges of this implementation
### Caching MSBuild results for re-use in subsequent Bazel Actions

MSBuild has a built-in mechanism to cache build results via its [static graph](https://github.com/dotnet/msbuild/blob/main/documentation/specs/static-graph.md#what-is-static-graph)
feature. This features, in Microsoft's own words, can be used:
> As part of a higher-order build system that uses single project isolated builds to provide 
> caching and/or distribution on top of the built-in functionality.

Bazel, in this case, is the "higher-order" build system. However, the built-in mechanism has a couple
limitations

#### 1. Preventing Duplication of work: MSBuild lacks of caching of intermediate build results

The default caching only caches the [explicitly requested build result](https://github.com/dotnet/msbuild/issues/5204#issuecomment-616845030), 
but targets can depend on intermediate results: see boxes with a purple border in the [target graph](./docs/HelloBazel.csproj.dot.svg).
```
    Build (green boxes) --> ... --> ResolveReferences
    PublishOnly (pink boxes) --> ... --> ResolveReferences
```

Without explicitly requesting "ResolveReferences" be built, the current implementation will discard
those results, and the next invocation of MSBuild will re-execute "ResolveReferences", duplicating 
work.

Because of Bazel's sandboxing which sets files to readonly, MSBuild Targets that generate files 
will not be able to open the file for writing, and the build will fail.  

#### 2. Supporting Sandboxing: MSBuild's caches write absolute paths to cache files

For many good reasons, MSBuild uses absolute paths to refer to files. This enables project files to 
use relative paths, while being located in completely places throughout the machine. 

For example: 
1. The dotnet sdk lives in `/usr/local/share/dotnet/sdk/<version>`
1. NuGet packages live in `~/.nuget/`
1. User Project files live anywhere.

MSBuild appears to cope with this by referring to every file with an absolute path. This clearly 
does not work well with Bazel sandboxing, since Bazel creates temporary file systems for an action 
to execute in. 

[It appears](https://github.com/dotnet/msbuild/issues/5204#issuecomment-629020643) that Microsoft's 
own CI machines accommodate these absolute paths by guaranteeing the same paths across build 
machines, and [BuildXL appears to have a PathRemapper](https://github.com/microsoft/BuildXL/blob/master/Public/Src/Utilities/Configuration/Mutable/PathRemapper.cs)
 integrated into the core of path processing to accommodate this as well.

Because of this, a file output by MSBuild might refer to another file by an absolute path that will 
not exist when the next Bazel action runs. 

#### 3. Limited Dependency Injection and public apis of the Programmatic Build Engine

Many of the [BuildManager's](https://github.com/dotnet/msbuild/blob/6eb3976d9a798ec1c546570c90c6ff1996b59c87/src/Build/BackEnd/BuildManager/BuildManager.cs),
methods and many other MSBuild's apis are marked `internal` and thus not available for customization 
or dependency injection from external code. 

To solve challenges 1 and 2, a [custom build manager](./dotnet/tools/builder) ensures all 
intermediate results are cached and takes care of mapping paths to and from the cache file for 
re-use in subsequent Bazel actions.  

For this custom build manager to get access to MSBuild's internal implementations for this, 
[SamHowes.Microsoft.Build](https://github.com/samhowes/SamHowes.Microsoft.Build) clones the 
[MSBuild repository](https://github.com/dotnet/msbuild) and makes some automated changes to the 
source code to make many classes `public` instead of `internal`, as well as enable some dependency 
injection of the PathMapper, before compiling into a NuGet package which is used in place of the 
standard [Microsoft.Build](https://www.nuget.org/packages/Microsoft.Build) NuGet package.

 

## Resources

1. This Starlark implementation is styled after the implementation of
   [rules_go](https://github.com/bazelbuild/rules_go)
1. JayConrod (from rules_go) did a great intro to implementing bazel rules in his blog post:
   [Writing Bazel rules](https://jayconrod.com/posts/106/writing-bazel-rules--simple-binary-rule)
1. [MSBuild Static Graph docs](https://github.com/dotnet/msbuild/blob/main/documentation/specs/static-graph.md)
1. [Limitations of Static Graph](https://github.com/dotnet/msbuild/issues/5204)

