load("//dotnet/private/actions:assembly.bzl", "emit_assembly", "make_launcher")
load("//dotnet/private/actions:publish.bzl", "publish")
load("//dotnet/private:providers.bzl", "DotnetLibraryInfo", "DotnetSdkInfo", "NuGetPackageInfo")
load("//dotnet/private:context.bzl", "dotnet_context", "dotnet_exec_context")
load("@bazel_skylib//lib:dicts.bzl", "dicts")

def _dotnet_tool_binary_impl(ctx):
    dotnet = dotnet_exec_context(ctx, True)

    info, outputs, private = emit_assembly(ctx, dotnet)
    return [
        DefaultInfo(
            files = depset([info.assembly]),
            executable = info.assembly,
        ),
        info,
        OutputGroupInfo(
            all = depset(outputs + private),
        ),
    ]

def _make_executable(ctx, test):
    dotnet = dotnet_exec_context(ctx, True, test)

    info, outputs, private = emit_assembly(ctx, dotnet)
    launcher = make_launcher(ctx, dotnet, info)

    launcher_info = ctx.attr._launcher_template[DefaultInfo]
    assembly_runfiles = ctx.runfiles(
        files = [info.output_dir] + ctx.files.data,
        transitive_files = depset([dotnet.sdk.dotnet]),
    )

    assembly_runfiles = assembly_runfiles.merge(launcher_info.default_runfiles)
    return [
        DefaultInfo(
            files = depset([launcher, info.output_dir]),
            runfiles = assembly_runfiles,
            executable = launcher,
        ),
        info,
        OutputGroupInfo(
            all = depset(outputs + private),
        ),
    ]

def _dotnet_binary_impl(ctx):
    return _make_executable(ctx, False)

def _dotnet_test_impl(ctx):
    return _make_executable(ctx, True)

def _dotnet_library_impl(ctx):
    dotnet = dotnet_exec_context(ctx, False)
    info, outputs, private = emit_assembly(ctx, dotnet)
    return [
        DefaultInfo(
            files = depset([info.assembly]),
        ),
        OutputGroupInfo(
            all = depset(outputs + private),
        ),
        info,
    ]

def _dotnet_publish_impl(ctx):
    return publish(ctx)

# Used by dotnet_tool_binary
BASE_ASSEMBLY_ATTRS = {
    "srcs": attr.label_list(allow_files = [".cs"]),
    "target_framework": attr.string(
        mandatory = True,
        doc = ("Target Framework Monikor (TFM) for the target .NET Framework i.e. netcoreapp3.1" +
               " https://docs.microsoft.com/en-us/dotnet/standard/frameworks"),
    ),
    "msbuild_properties": attr.string_dict(
        doc = "Properties to be placed in the main <PropertyGroup> of the generated project file.",
    ),
    "sdk": attr.string(
        default = "Microsoft.NET.Sdk",
        doc = """The dotnet sdk to use, normally found in the project element like `<Project Sdk="{sdk}">`. Most common
        values are `Microsoft.NET.Sdk` (the default) and `Microsoft.NET.Sdk.Web`. This string will be substituted
        directly into the `Sdk` attribute.

        If sandboxing is enabled, and the specified sdk has not been specified for download as a NuGet package, then it
        may not be available at compile time. See https://github.com/samhowes/my_rules_dotnet/issues/78 for updates.
         """,
    ),
    "_project_template": attr.label(
        default = Label("//dotnet/private/msbuild:project.tpl.proj"),
        allow_single_file = True,
    ),
}

dotnet_tool_binary = rule(
    implementation = _dotnet_tool_binary_impl,
    attrs = dicts.add(BASE_ASSEMBLY_ATTRS, {
        "dotnet_sdk": attr.label(
            mandatory = True,
            providers = [DotnetSdkInfo],
        ),
    }),
    executable = True,
    doc = """Used instead of dotnet_binary for executables in the toolchain.

dotnet_tool_binaries cannot have any dependencies and are used to build other dotnet_* targets.""",
)

# used by all end-user assemblies (libraries, tests & binaries)
ASSEMBLY_ATTRS = dicts.add(BASE_ASSEMBLY_ATTRS, {
    "_dotnet_context_data": attr.label(default = "//:dotnet_context_data"),
    "data": attr.label_list(
        allow_files = True,
        doc = """Standard bazel runfiles data. These files will be made available in the runfiles tree at the location
        bazel normally places runfiles.""",
    ),
    "content": attr.label_list(
        allow_files = True,
        allow_empty = True,
        doc = """Data items that will be copied to the output directory directly adjacent to the application assembly.
        Items in this list are equivalent to specifying
        `<Content Include="..."><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>` in the project file.
        These files will be available in a sandbox, but MSBuild will not be made aware of their existence.
        """,
    ),
    "deps": attr.label_list(
        providers = [
            [DotnetLibraryInfo],
            [NuGetPackageInfo],
        ],
    ),
})

dotnet_library = rule(
    _dotnet_library_impl,
    attrs = ASSEMBLY_ATTRS,
    executable = False,
    toolchains = ["@my_rules_dotnet//dotnet:toolchain"],
)

# used only by executables i.e. tests and standard binaries
EXECUTABLE_ATTRS = dicts.add(ASSEMBLY_ATTRS, {
    "_launcher_template": attr.label(
        default = Label("//dotnet/tools/launcher"),
        allow_single_file = True,
    ),
})

dotnet_binary = rule(
    implementation = _dotnet_binary_impl,
    attrs = EXECUTABLE_ATTRS,
    executable = True,
    toolchains = ["@my_rules_dotnet//dotnet:toolchain"],
)

dotnet_publish = rule(
    implementation = _dotnet_publish_impl,
    attrs = {
        "target": attr.label(
            doc = "The dotnet_* target to publish.",
            mandatory = True,
        ),
    },
    toolchains = ["@my_rules_dotnet//dotnet:toolchain"],
)

dotnet_test = rule(
    _dotnet_test_impl,
    attrs = EXECUTABLE_ATTRS,
    test = True,
    toolchains = ["@my_rules_dotnet//dotnet:toolchain"],
)
