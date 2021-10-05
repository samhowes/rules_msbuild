"""
Definitions: (some are made up)
- Canonical Name: The PascalCase name of the package as it would be displayed on a web page, i.e. `CommandLineParser`
- Requested Name: The name that the user listed as part of the key of the dictionary passed to `nuget_fetch`
    this can be the Canonical Name with _any casing_ i.e. `ComManDlIneParser`
- Version String: A specific NuGet version string i.e. `2.9.0-preview1`
- Version Spec: A string that can be resolved to a Version String by NuGet that is entered by the user as the second
    half of the key to the dictionary passed to nuget_fetch. i.e. `2.9.*`. Currently, only precise version specs are
    supported. i.e. `2.9.0`.
    Other examples: `[2.4.1]`
- Package Id (pkg_id): CanonicalName/VersionString i.e. `CommandLineParser/2.9.0-preview1`
- Package Id Lower (pkg_id_lower): canonicalname/versionstring i.e. `commandlineparser/2.9.0-preview1`
- Target Framework Moniker (tfm): An identifier used to indicate the target framework in a .csproj file and in a build
    output folder i.e. `netcoreapp3.1`. The user is likely familiar with this string. It is well documented.
- Target Framework Identifier (tfi): An identifier used to indicate the target framework in some rare cases in the build
    process by NuGet and maybe MSBuild. i.e. `.NETCoreApp,Version=v3.1` It is not well documented.
- NuGet File Group: A grouping of files in a NuGet package. The group name, i.e. `compile` or `runtime` indicates what
    phase of the build the files are used in.
    https://docs.microsoft.com/en-us/nuget/create-packages/creating-a-package#from-a-convention-based-working-directory
    Background on reference assemblies vs implementation assemblies:
    https://github.com/dotnet/standard/blob/master/docs/history/evolution-of-design-time-assemblies.md
    group names:
        - compile: files needed at compile time. Folders seen: `ref`, `lib`. I assume `ref` files are reference
            assemblies that are not needed at runtime.
                System.Xml.XDocument/4.3.0 has different `ref` and `runtime` files.
        - runtime: files are often in the `lib` folder, these get copied to the output directory.
            Note: these are not "runfiles", but instead assemblies loaded at runtime i.e. implementation assemblies.
        - build: this group can contain .targets, .props, and other files. These files are used by the project build
            system.
        - runtimeTargets: files for specific runtimes that are being targeted. Items in this group are identified by
            runtimes/<rid>/<group>/<tfm>/Package.Name.dll. Tfm in this case is the tfm of the package that supports
            the tfm being built. i.e. if netcoreapp3.1 is being built, then tfm could refer to netstandard1.6.
            Example file references:
                runtimes/opensuse.42.1-x64/native/System.Security.Cryptography.Native.OpenSsl.so
                runtimes/osx.10.10-x64/native/System.Security.Cryptography.Native.Apple.dylib
        - resource: appears to be resource dlls for different locales. The value of the dictionary is a dictionary of
            locales supported.

"""

load("@bazel_skylib//lib:paths.bzl", "paths")
load(
    "//dotnet/private/msbuild:xml.bzl",
    "STARTUP_DIR",
    "prepare_project_file",
)
load("//dotnet/private/msbuild:nuget.bzl", "NUGET_BUILD_CONFIG", "prepare_nuget_config")
load("//dotnet/private/toolchain:common.bzl", "default_tfm", "detect_host_platform")
load("//dotnet/private:providers.bzl", "DEFAULT_SDK", "MSBuildSdk")
load("//dotnet/private:context.bzl", "dotnet_context", "make_cmd")

_TfmInfo = provider(fields = ["tfn", "implicit_deps"])

def nuget_deps_helper(frameworks, packages):
    """Convert frameworks and packages into a single list that is consumable by the nuget_fetch deps attribute

    A list item is in the format "<package_name>/<version>:tfm,tfm,tfm".
    For a list of frameworks, we'll just omit the left half of the item.
    """
    res = [",".join(frameworks)]
    for (pkg, tfms) in packages.items():
        res.append(pkg + ":" + ",".join(tfms))
    return res

def _nuget_fetch_impl(ctx):
    config = struct(
        fetch_base = ctx.path("fetch"),
        parser_base = ctx.path("fetch/parser"),
        intermediate_base = ctx.path("fetch/_obj"),
        bazel_packages = ctx.path("bazel_packages"),
        packages_folder = "packages",
        fetch_config = ctx.path("NuGet.Fetch.Config"),
        packages_by_tfm = {},  # dict[tfm, dict[pkg_id_lower: _pkg]] of requested packages
        packages = {},  # {pkg_name/version: _pkg} where keys are in all lowercase
    )
    os, _ = detect_host_platform(ctx)
    dotnet = dotnet_context(
        str(ctx.path(ctx.attr.dotnet_sdk_root).dirname),
        os,
    )

    _fetch_custom_packages(ctx, config)
    _configure_host_packages(ctx, dotnet, config)

    _generate_nuget_configs(ctx, config)
    parser_project = _copy_parser(ctx, config)

    spec = ctx.attr.deps + nuget_deps_helper(ctx.attr.target_frameworks, ctx.attr.packages)

    spec_path = config.fetch_base.get_child("spec.txt")
    ctx.file(spec_path, "\n".join(spec))

    substitutions = prepare_project_file(
        None,
        paths.join(str(config.intermediate_base.basename), "$(MSBuildProjectName)"),
        [],
        [],
        config.fetch_config,
        None,  # no tfm for the traversal project
        exec_root = STARTUP_DIR,
    )
    props = config.fetch_base.get_child("Restore.props")
    ctx.template(
        props,
        ctx.attr._master_template,
        substitutions = substitutions,
    )

    if "foo" != "bar":
        pass
    args = [
        dotnet.path,
        "run",
        "--property:BazelFetch=true",
        "--",
        "--spec_path=" + str(spec_path),
        "--dotnet_path=%s" % dotnet.path,
        "--packages_folder=%s" % str(ctx.path(config.packages_folder)),
        "--test_logger=%s" % ctx.attr.test_logger,
        "--nuget_build_config=%s" % NUGET_BUILD_CONFIG,
    ]
    ctx.report_progress("Fetching NuGet packages")

    result = ctx.execute(
        args,
        environment = dotnet.env,
        quiet = True,
        working_directory = str(parser_project.dirname),
    )
    if result.return_code != 0:
        fail("failed executing '{}':\nstdout: {}\nstderr: {}".format(" ".join(args), result.stdout, result.stderr))

def _configure_host_packages(ctx, dotnet, config):
    if not ctx.attr.use_host:
        return

    # https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-locals
    cache_type = "global-packages"
    args = [
        dotnet.path,
        "nuget",
        "locals",
        cache_type,
        "--list",
    ]

    result = ctx.execute(args)
    if result.return_code != 0:
        fail("failed to find global-packages folder with dotnet: " + result.stderr)

    # example dotnet5 output: `global-packages: /Users/samh/.nuget/packages/`
    # example dotnet3.1 output: `info : global-packages: /Users/samh/.nuget/packages/`
    ind = result.stdout.index(cache_type)
    if ind < 0:
        fail("unexpected output from {}: {}".format(" ".join(args), result.stdout))
    start = ind + len(cache_type) + 2
    location = result.stdout[start:].strip()

    # it's possible that the packages folder doesn't exist yet, if it doesn't the symlink won't be functional
    # this mostly likely won't be the case in actual usage, but is definitely possible if the folder has been
    # cleaned, like on a fresh CI instance for example.
    mkdir = None
    if dotnet.os == "windows":
        mkdir = ["cmd", "/e:on", "/c", "mkdir " + location]
    else:
        mkdir = ["mkdir", "-p", location]

    ctx.execute(mkdir)
    ctx.symlink(location, config.packages_folder)

def _copy_parser(ctx, config):
    parser_path = ctx.path(ctx.attr._parser_project)
    for f in parser_path.dirname.readdir():
        if f.basename in {"bin": True, "obj": True}:
            continue
        contents = ctx.read(f)
        ctx.file(config.parser_base.get_child(f.basename), contents, legacy_utf8 = False)

    return config.parser_base.get_child(parser_path.basename)

def _fetch_custom_packages(ctx, config):
    ctx.download(
        "https://github.com/samhowes/SamHowes.Microsoft.Build/releases/download/0.0.1/SamHowes.Microsoft.Build.16.9.0.nupkg",
        output = config.bazel_packages.get_child("SamHowes.Microsoft.Build.16.9.0.nupkg"),
        sha256 = "e6618ec0f9fa91c2ffb7ad0dd7758417e0cf97e1da6a54954834f3cb84b56c2d",
    )

def _generate_nuget_configs(ctx, config):
    substitutions = prepare_nuget_config(
        config.packages_folder,
        True,
        # todo(#46) allow custom package sources
        [
            {"key": "nuget.org", "value": "https://api.nuget.org/v3/index.json", "protocolVersion": "3"},
            {"key": "bazel", "value": config.bazel_packages.realpath},
        ],
    )
    ctx.template(
        ctx.path(config.fetch_config),
        ctx.attr._config_template,
        substitutions = substitutions,
    )

    substitutions = prepare_nuget_config(
        config.packages_folder,
        False,  # no fetch allowed at build time
        [],  # don't even add sources, just in case
    )
    ctx.template(
        ctx.path(NUGET_BUILD_CONFIG),
        Label("@rules_msbuild//dotnet/private/msbuild:NuGet.tpl.config"),
        executable = False,
        substitutions = substitutions,
    )

nuget_fetch = repository_rule(
    implementation = _nuget_fetch_impl,
    attrs = {
        "packages": attr.string_list_dict(),
        "test_logger": attr.string(
            default = "JunitXml.TestLogger/3.0.87",
        ),
        # todo(#63) link this to the primary nuget folder if it is not the primary nuget folder
        "dotnet_sdk_root": attr.label(
            default = Label("@dotnet_sdk//:ROOT"),
        ),
        "use_host": attr.bool(
            default = False,
            doc = ("When false (default) nuget packages will be fetched into a bazel-only directory, when true, the " +
                   "host machine's global packages folder will be used. This is determined by executing " +
                   "`dotnet nuget locals global-packages --list"),
        ),
        "target_frameworks": attr.string_list(mandatory = True),
        "deps": attr.string_list(
            doc = ("Use nuget_deps_helper to specify target_frameworks and nuget packages for workspaces that you " +
                   "depend on"),
        ),
        "_master_template": attr.label(
            default = Label("@rules_msbuild//dotnet/private/msbuild:project.tpl.proj"),
        ),
        "_tfm_template": attr.label(
            default = Label("@rules_msbuild//dotnet/private/msbuild:project.tpl.proj"),
        ),
        "_config_template": attr.label(
            default = Label("@rules_msbuild//dotnet/private/msbuild:NuGet.tpl.config"),
            allow_single_file = True,
        ),
        "_nuget_import_template": attr.label(
            default = Label("@rules_msbuild//dotnet/private/toolchain:BUILD.nuget_import.bazel"),
            allow_single_file = True,
        ),
        "_root_template": attr.label(
            default = Label("@rules_msbuild//dotnet/private/toolchain:BUILD.nuget.bazel"),
            allow_single_file = True,
        ),
        "_parser_project": attr.label(
            default = Label("@rules_msbuild//dotnet/tools/NuGetParser:NuGetParser.csproj"),
        ),
        "_parser_srcs": attr.label(
            default = Label("@rules_msbuild//dotnet/tools/NuGetParser:NuGetParser_srcs"),
        ),
    },
)
