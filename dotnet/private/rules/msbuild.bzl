load("//dotnet/private:providers.bzl", "DotnetLibraryInfo", "DotnetRestoreInfo", "DotnetSdkInfo", "MSBuildDirectoryInfo", "NuGetPackageInfo")
load("//dotnet/private:context.bzl", "dotnet_exec_context")
load("//dotnet/private/actions:restore.bzl", "restore")
load("//dotnet/private/actions:publish.bzl", "publish")
load("//dotnet/private/actions:tool_binary.bzl", "build_tool_binary")
load("//dotnet/private/actions:assembly.bzl", "build_assembly")
load("//dotnet/private/actions:launcher.bzl", "make_launcher")
load("@bazel_skylib//lib:dicts.bzl", "dicts")

TOOLCHAINS = ["@rules_msbuild//dotnet:toolchain"]

def _msbuild_tool_binary_impl(ctx):
    dotnet = dotnet_exec_context(ctx, True)

    info, all_outputs = build_tool_binary(ctx, dotnet)
    return [
        DefaultInfo(
            files = depset([info.output_dir]),
        ),
        info,
        OutputGroupInfo(
            all = depset(all_outputs),
        ),
    ]

def _publish_impl(ctx):
    info = publish(ctx)
    all = depset([info.output_dir])
    return [
        DefaultInfo(files = all),
        info,
        OutputGroupInfo(all = all),
    ]

def _restore_impl(ctx):
    dotnet = dotnet_exec_context(ctx, False)
    restore_info, outputs = restore(ctx, dotnet)
    return [
        DefaultInfo(
            files = depset([restore_info.output_dir]),
        ),
        restore_info,
        OutputGroupInfo(
            all = depset(outputs),
        ),
    ]

def _binary_impl(ctx):
    return _make_executable(ctx, False)

def _test_impl(ctx):
    return _make_executable(ctx, True)

def _make_executable(ctx, is_test):
    dotnet = dotnet_exec_context(ctx, True, is_test)
    info, outputs = build_assembly(ctx, dotnet)
    launcher = make_launcher(ctx, dotnet, info)

    launcher_info = ctx.attr._launcher_template[DefaultInfo]
    assembly_runfiles = ctx.runfiles(
        files = [info.output_dir] + ctx.files.data,
        transitive_files = depset([dotnet.sdk.dotnet], transitive = [info.runfiles]),
    )

    assembly_runfiles = assembly_runfiles.merge(launcher_info.default_runfiles)

    return [
        DefaultInfo(
            files = depset([launcher, info.assembly]),
            runfiles = assembly_runfiles,
            executable = launcher,
        ),
        info,
        OutputGroupInfo(
            all = outputs,
        ),
    ]

def _library_impl(ctx):
    dotnet = dotnet_exec_context(ctx, False)
    info, outputs = build_assembly(ctx, dotnet)
    return [
        DefaultInfo(
            files = depset([info.assembly]),
            runfiles = ctx.runfiles(transitive_files = info.runfiles),
        ),
        info,
        OutputGroupInfo(
            all = outputs,
        ),
    ]

_COMMON_ATTRS = {
    "assembly_name": attr.string(doc = """Assembly name to use. If not specified <name>.dll will be produced."""),
    "project_file": attr.label(
        doc = """The project file for MSBuild to build. If not specified, a combination of `name` and the `srcs`
attribute will be used to infer the project file.

For example:
```python
# implicitly specifies `Foo.csproj`
msbuild_binary(
    name = "Foo",
    srcs = ["Program.cs"],
)
```

If `project_file` **is** specified, and the srcs attribute is not specified, the proper file extension will be inferred from the project
extension, and that file extension will be globbed.
```python
# implicitly sets `srcs = glob(["**/*.cs"])`
msbuild_binary(
    name = "Foo",
    project_file = "Foo.csproj",
)
```
> Note: while omitting the srcs attribute adheres to the [Sdk Project file semantics](https://docs.microsoft.com/en-us/dotnet/core/project-sdk/overview#default-includes-and-excludes),
> Using globs is inefficient for bazel, as bazel will have to query the file system for the list of files.
""",
        allow_single_file = True,
        mandatory = True,
    ),
}

msbuild_tool_binary = rule(
    implementation = _msbuild_tool_binary_impl,
    attrs = dicts.add(_COMMON_ATTRS, {
        "srcs": attr.label_list(allow_files = True),
        "target_framework": attr.string(),
        "dotnet_sdk": attr.label(
            mandatory = True,
            providers = [DotnetSdkInfo],
        ),
        "deps": attr.label_list(
            providers = [NuGetPackageInfo],
        ),
        "_bazel_packages": attr.label(default = "@nuget//:bazel_packages", allow_files = True),
    }),
    # this is compiling a dotnet executable, but it'll be a framework dependent executable, so bazel won't be able
    # to execute it directly
    executable = False,
)

msbuild_publish = rule(
    _publish_impl,
    attrs = dicts.add(_COMMON_ATTRS, {
        "target": attr.label(mandatory = True, providers = [DotnetLibraryInfo]),
    }),
    executable = False,
    toolchains = TOOLCHAINS,
)

_RESTORE_COMMON_ATTRS = dicts.add(_COMMON_ATTRS, {
    "target_framework": attr.string(
        doc = """The [Target Framework Moniker (TFM)](https://docs.microsoft.com/en-us/dotnet/standard/frameworks#supported-target-frameworks)
of the target framework to compile for, i.e. `net5.0`, `netcoreapp3.1`, `netstandard2.0` etc.

* **Must** match the evaluated `<TargetFramework>` property in the project file
* **Must** be listed in the `target_frameworks` attribute of the `nuget_fetch` call for the workspace
* **Must not** be a target framework alias i.e. `net5.0-windows` see [issue #153](https://github.com/samhowes/rules_msbuild/issues/153)

If this target has NuGet dependencies, this TFM **must** be listed for restore in the `nuget_fetch`
call for the workspace.
```
msbuild_library(
    name = "Foo",
    srcs = ["Bar.cs"],
    target_framework = "net5.0",
    deps = ["@nuget//NewtonSoft.Json"],
)
```
Requires that the following must be specified:
```
nuget_fetch(
    name = "nuget",
    packages = {
        "NewtonSoft.Json/13.0.1": ["net5.0"],
    },
    target_frameworks = ["net5.0"],
)
```
> Note: `@rules_msbuild//gazelle/dotnet` will maintain the `target_framework` and `nuget_fetch` rule for you: After
> editing your project file, run `bazel run //:gazelle` (assuming your workspace was set up by `SamHowes.Bzl`).
""",
        mandatory = True,
    ),
})

RESTORE_ATTRS = dicts.add(_RESTORE_COMMON_ATTRS, {
    "msbuild_directory": attr.label(
        mandatory = True,
        providers = [MSBuildDirectoryInfo],
        doc = """The msbuild_directory to use, defaults to //:msbuild_defaults (generated by gazelle and samhowes.bzl.

This specifies Directory.Build.props and Directory.Build.targets to allow you to customize your build.""",
    ),
    "deps": attr.label_list(providers = [
        [DotnetRestoreInfo],
        [NuGetPackageInfo],
    ]),
    "version": attr.string(),
    "package_version": attr.string(),
})

msbuild_restore = rule(
    _restore_impl,
    attrs = RESTORE_ATTRS,
    executable = False,
    toolchains = TOOLCHAINS,
)

_ASSEMBLY_ATTRS = dicts.add(_RESTORE_COMMON_ATTRS, {
    "srcs": attr.label_list(
        doc = """Files to compile into the DLL: .cs, .fs, or .vb files.

If `project_file` is not specified, the extension of these files will be used to infer the project name.
i.e. given:
```
msbuild_binary(name = "Foo", srcs = ["Program.cs"], target_framework = "net5.0")
```

rules_msbuild will attempt to compile `Foo.csproj`.
""",
        allow_files = True,
    ),
    "lang": attr.string(
        doc = """oneof (cs, fs, vb): If project_file and srcs are both absent, infer the attributes using this language

i.e. given:
```
msbuild_binary(name = "Foo", lang = "fs", target_framework = "net5.0")
```

rules_msbuild will attempt to compile `Foo.fsproj` by globbing for `**/*.fs` files.
""",
    ),
    "restore": attr.label(
        doc = "The restore target that this assembly depends on: `<name>_restore.`",
        mandatory = True,
        providers = [DotnetRestoreInfo],
    ),
    "data": attr.label_list(
        doc = """List of runfiles to be available at runtime. Use with `@rules_msbuild//dotnet/tools/Runfiles`.""",
        allow_files = True,
    ),
    "content": attr.label_list(
        doc = """List of files to make available to MSBuild when executing Build and Publish targets.

ex: `content = ["appsettings.json]`
Corresponds to the `Content` Item type specified in a project file.
Setting this attribute does **not** impact MSBuild behavior, it only includes the files as inputs for the bazel
action, i.e. the target will be rebuilt if these files change.

To configure MSBuild behavior, such as for setting `<CopyToOutputDirectory>Always</CopyToOutputDirectory>`, use
XML in the project file as usual.

> Note: If a content file changes, bazel will re-execute the action and recompile the assembly. For maximum build
> caching, consider using the `data` attribute and the `@rules_msbuild//dotnet/tools/Runfiles` library instead. The
> `content` attribute is available only for familiar MSBuild semantics.
""",
        allow_files = True,
    ),
    "protos": attr.label_list(
        doc = """List of `proto_library` targets that this assembly depends on.

To compile those protos, use the [grpc-dotnet nuget package](https://github.com/grpc/grpc-dotnet#grpc-for-net) and
specify the proto in a `<Protobuf Include="<proto_file>"/>` element in your project file [as per the grpc-dotnet docs](https://docs.microsoft.com/en-us/aspnet/core/grpc/?view=aspnetcore-5.0#c-tooling-support-for-proto-files-1).

See [@rules_msbuild//tests/examples/Grpc](//tests/examples/Grpc:Readme.md) for examples.
""",
        providers = [ProtoInfo],
        default = [],
    ),
    "deps": attr.label_list(
        doc = """The deps of this assembly. Must be a `rules_msbuild` assembly or a nuget package.

If your project file references a project not listed, the build will fail.

> Note: `@rules_msbuild//gazelle/dotnet` will maintain the `deps` attribute for you: After editing your project file,
> run `bazel run //:gazelle` (assuming your workspace was set up by `SamHowes.Bzl`).
""",
        providers = [
            [DotnetLibraryInfo],
            [NuGetPackageInfo],
        ],
    ),
})

msbuild_library = rule(
    _library_impl,
    attrs = _ASSEMBLY_ATTRS,
    executable = False,
    toolchains = TOOLCHAINS,
)

_EXECUTABLE_ATTRS = dicts.add(_ASSEMBLY_ATTRS, {
    "_launcher_template": attr.label(
        default = Label("//dotnet/tools/launcher"),
        allow_single_file = True,
    ),
})

msbuild_binary = rule(
    _binary_impl,
    attrs = _EXECUTABLE_ATTRS,
    executable = True,
    toolchains = TOOLCHAINS,
)

msbuild_test = rule(
    _test_impl,
    attrs = dicts.add(_EXECUTABLE_ATTRS, {
        "dotnet_cmd": attr.string(default = "test"),
        "test_env": attr.string_dict(),
    }),
    executable = True,
    test = True,
    toolchains = TOOLCHAINS,
)
