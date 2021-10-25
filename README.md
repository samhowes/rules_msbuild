# Dotnet Rules for Bazel

| Windows                                                                                                                                                                                                                                                        | Mac                                                                                                                                                                                                                                                    | Linux                                                                                                                                                                                                                                                      |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [![Build Status](https://dev.azure.com/samhowes/rules_msbuild/_apis/build/status/samhowes.rules_msbuild?branchName=master&jobName=windows)](https://dev.azure.com/samhowes/rules_msbuild/_build/latest?definitionId=6&branchName=master&jobName=windows) | [![Build Status](https://dev.azure.com/samhowes/rules_msbuild/_apis/build/status/samhowes.rules_msbuild?branchName=master&jobName=mac)](https://dev.azure.com/samhowes/rules_msbuild/_build/latest?definitionId=6&branchName=master&jobName=mac) | [![Build Status](https://dev.azure.com/samhowes/rules_msbuild/_apis/build/status/samhowes.rules_msbuild?branchName=master&jobName=linux)](https://dev.azure.com/samhowes/rules_msbuild/_build/latest?definitionId=6&branchName=master&jobName=linux) |

<!--
Links
 -->
> These docs are under construction. Please open an issue for any specific questions!

rules_msbuild is an alternative to [rules_dotnet](https://github.com/bazelbuild/rules_dotnet).

# In Beta!
```bash
# set up a hello world dotnet app
mkdir HelloBazelDotnet && cd HelloBazelDotnet 
dotnet new console -o AwesomeExecutable --no-restore
dotnet new sln && dotnet sln add AwesomeExecutable
 
dotnet tool install -g SamHowes.Bzl     # installs dotnet cli tool `bzl`
bzl                              # automatically generate a WORKSPACE and ide integration files
bazel run //:gazelle             # generate build files with custom Gazelle language
bazel build //...                # use bazel to build .csproj, .fsproj, or .vbproj files
bazel run //AwesomeExecutable    # => Hello World!
```

Check out the `tests/` directory & `e2e/` directory for examples

## Features
1. Build .csproj files with Bazel
1. `dotnet build` feature parity
1. IDE Integration with JetBrains Rider / Visual Studio with no custom plugins
1. Automated BUILD file generation via [bazel gazelle](https://github.com/bazelbuild/bazel-gazelle)
1. Runfiles Library
1. Bazel sandboxing compatible
1. [Grpc & Proto support](./tests/examples/Grpc) Out of the Box via [grpc-dotnet](https://github.com/grpc/grpc-dotnet)
1. No third party workspace dependencies

# Contents
1. Overview 
    1. [Usage](#usage)
        1. [msbuild_library](#msbuild_library)
        1. [msbuild_binary](#msbuild_binary)
        1. [NuGet Dependencies](#nuget-dependencies)
    1. [Sharp Edges](#watch-out-for-sharp-edges)
1. [Build File Generation with Gazelle](gazelle/dotnet/Readme.md)<!-- toc:start -->
1. [Rules](docs/rules.md)<!-- toc:end -->
1. [Implementation Details](docs/ImplementationDetails.md)

# Usage

> Note: [SamHowes.Bzl](https://www.nuget.org/packages/SamHowes.Bzl/) and using `bazel run //:gazelle` (as described above) is strongly recommended. 
## Workspace
```python
# //WORKSPACE
load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")
http_archive(
    name = "rules_msbuild",
    sha256 = "96df9be286fff1fadf61f46f64065158a2a1bb8d2e61f39d4ec4affa443012a9",
    urls = ["https://github.com/samhowes/rules_msbuild/releases/download/0.0.8/rules_msbuild-0.0.8.tar.gz"],
)
load("@rules_msbuild//dotnet:deps.bzl", "msbuild_register_toolchains", "msbuild_rules_dependencies")

msbuild_rules_dependencies()
# See https://dotnet.microsoft.com/download/dotnet for valid versions
msbuild_register_toolchains(version = "host")
```
## Compiling Assemblies
`msbuild_library` and `msbuild_binary` are macros that compile [framework dependent](https://andrewlock.net/should-i-use-self-contained-or-framework-dependent-publishing-in-docker-images/) assemblies that can be run with `dotnet run`. The macros define the targets `<name>_restore`, `<name>`, and `<name>_publish` 
labels. 

Given a .csproj file located at //console:console.csproj with a TargetFramework of net5.0, invoking 
`bazel  build //console/console_publish` will result in `bazel-bin/console/publish/net5.0/console.dll`
that can be run with `dotnet exec console.dll`, and `bazel run //console` will run the executable 
under all the standard bazel expectations.

The `//:gazelle` rule generated by `samhowes.bzl` will generate all the necessary build files from 
any .csproj, .fsproj, or .vbproj files that you have in your repository.  

### msbuild_library
```python
# //ClassLibrary/BUILD
load("@rules_msbuild//dotnet:defs.bzl", "msbuild_library")

# expects ClassLibrary.csproj to exist
msbuild_library(
    name = "ClassLibrary",
    srcs = ["Utility.cs"],                  # srcs can be explicitly specified
    target_framework = "netstandard2.1",
    deps = [
        "@nuget//NewtonSoft.Json",
    ],
)
```
### msbuild_binary
```python
# //Console/BUILD
load("@rules_msbuild//dotnet:defs.bzl", "msbuild_binary")

msbuild_binary(
    name = "hello",                        # adds the property AssemblyName="hello"
    # omitting srcs automatically globs the directory for source files
    project_file = "Console.csproj",       # project_file is specified when AssemblyName is different
    target_framework = "netcoreapp3.1",
    deps = [
        "//ClassLibrary",
        "@nuget//NewtonSoft.Json",
    ],
)
```

### NuGet dependencies

`@rules_msbuild//gazelle/dotnet` automatically manages your nuget dependencies by parsing your 
project files. 

NuGet packages are represented by a PackageId and a list of frameworks that must be
restored for that PackageId. Multiple nuget package versions can be specified.

```python
# //WORKSPACE
load("//deps:nuget.bzl", "nuget_deps")
nuget_deps()
```

```python
# //deps:nuget.bzl

load("@rules_msbuild//deps:public_nuget.bzl", "FRAMEWORKS", "PACKAGES")
load("@rules_msbuild//dotnet:defs.bzl", "nuget_deps_helper", "nuget_fetch")

def nuget_deps():
    nuget_fetch(
        name = "nuget",
        packages = {
            "Newtonsoft.Json/13.0.1": ["net5.0", "netstandard2.1"],
        },
        target_frameworks = ["net5.0", "netstandard2.1"],
        # use_host = True will use the global packages folder
        # use_host = False will download all packages to the bazel nuget workspace folder in isolation
        use_host = True,
        # rules_msbuild requires some nuget packages to run, these do not affect your workspaces
        # NuGet package versions
        # if you depend on other workspaces that need nuget packages, you can add them here
        deps = nuget_deps_helper(FRAMEWORKS, PACKAGES),
    )
```

# Watch out for sharp edges!
These rules are still in "beta" and the core functionality is still being refined. 

These rules assume you have used `dotnet tool install -g samhowes.bzl` to set up your workspace  and
 run `bazel run //:gazelle` after adding any source files, nuget packages, or project references.
 
Any issues with the label [sharp-edge](https://github.com/samhowes/rules_msbuild/issues?q=is%3Aissue+is%3Aopen+label%3Asharp-edge) are specifically known to be confusing and  make working with these rules 
hard.

Specifically:
1. If you add a project reference, nuget package, or source file make sure you run 
    `bazel run //:gazelle`. The error messages are currently [not very clear](https://github.com/samhowes/rules_msbuild/issues/159) when bazel doesn't know about a particular input file.
1. If a machine doesn't have a dotnet sdk/runtime installed, and a project file targets a framework 
    version defined by that sdk/runtime, then a [weird error message](https://github.com/samhowes/rules_msbuild/issues?q=is%3Aissue+is%3Aopen+label%3Asharp-edge) will be output by the NuGetparser, or the 
    builder when restoring packages for that framework version.
1. [Annoying build warnings](https://github.com/samhowes/rules_msbuild/issues/148) will be output when bazel is building the "builder", these can be 
    ignored.