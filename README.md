# Dotnet Rules for Bazel

| Windows                                                                                                                                                                                                                                                        | Mac                                                                                                                                                                                                                                                    | Linux                                                                                                                                                                                                                                                      |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [![Build Status](https://dev.azure.com/samhowes/rules_msbuild/_apis/build/status/samhowes.rules_msbuild?branchName=master&jobName=windows)](https://dev.azure.com/samhowes/rules_msbuild/_build/latest?definitionId=3&branchName=master&jobName=windows) | [![Build Status](https://dev.azure.com/samhowes/rules_msbuild/_apis/build/status/samhowes.rules_msbuild?branchName=master&jobName=mac)](https://dev.azure.com/samhowes/rules_msbuild/_build/latest?definitionId=3&branchName=master&jobName=mac) | [![Build Status](https://dev.azure.com/samhowes/rules_msbuild/_apis/build/status/samhowes.rules_msbuild?branchName=master&jobName=linux)](https://dev.azure.com/samhowes/rules_msbuild/_build/latest?definitionId=3&branchName=master&jobName=linux) |

<!--
Links
 -->
> These docs are under construction. If you are looking for a released version of building dotnet
> with Bazel, head over to [bazelbuild/rules_dotnet](https://github.com/bazelbuild/rules_dotnet)

# Coming soon...
```bash
# set up a hello world dotnet app
mkdir -p HelloBazelDotnet/AwesomeExecutable && cd HelloBazelDotnet/AwesomeExecutable
dotnet new console --no-restore
cd .. && dotnet new sln && dotnet sln add AwesomeExecutable
 
dotnet tool install -g SamHowes.Bzl
bzl init                         # automatically configure your workspace
bazel run //:gazelle             # generate build files with custom Gazelle language
bazel build //...                # use bazel to build .csproj files
bazel run //AwesomeExecutable    # => Hello World!
```

Check out the `tests/` directory & `e2e/` directory for examples

## Features
* Build .csproj files with Bazel
* `dotnet build` feature parity 
* IDE Integration with JetBrains Rider / Visual Studio by default
* Automated BUILD file generation
* Runfiles Library

### Blockers
1. [End to End Testing on Windows](https://github.com/samhowes/rules_msbuild/pull/152)
1. [Backlog](https://github.com/samhowes/rules_msbuild/projects/4)
1. Updating docs


# (Future) Usage

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

## Resources

1. This implementation is styled after the implementation of
   [rules_go](https://github.com/bazelbuild/rules_go)
1. JayConrod (from rules_go) did a great intro to implementing bazel rules in his blog post:
   [Writing Bazel rules](https://jayconrod.com/posts/106/writing-bazel-rules--simple-binary-rule)

