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
    "prepare_nuget_config",
    "prepare_project_file",
)
load("//dotnet/private/toolchain:common.bzl", "detect_host_platform")
load("//dotnet/private:providers.bzl", "DEFAULT_SDK", "MSBuildSdk")
load("//dotnet/private:context.bzl", "dotnet_context", "make_cmd")

NUGET_BUILD_CONFIG = "NuGet.Build.Config"

def _nuget_fetch_impl(ctx):
    config = struct(
        fetch_base = "fetch",
        intermediate_base = "_obj",
        packages_folder = "packages",
        fetch_config = ctx.path("NuGet.Fetch.Config"),
        packages_by_tfm = {},  # dict[tfm, dict[pkg_id_lower: _pkg]] of requested packages
        packages = {},  # {pkg_name/version: _pkg} where keys are in all lowercase
        all_files = [],
        tfm_mapping = {},
    )
    os, _ = detect_host_platform(ctx)
    dotnet = dotnet_context(
        str(ctx.path(ctx.attr.dotnet_sdk_root).dirname),
        os,
    )

    if ctx.attr.use_host:
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
        ctx.execute(["mkdir", location])
        ctx.symlink(location, config.packages_folder)

    _generate_nuget_configs(ctx, config)
    fetch_project = _generate_fetch_project(ctx, config)

    args, _ = make_cmd(
        dotnet,
        paths.basename(str(fetch_project)),
        "restore",
        True,  # todo(#51) determine when to binlog
    )
    args = [dotnet.path] + args
    ctx.report_progress("Fetching NuGet packages for frameworks: {}".format(", ".join(config.packages_by_tfm.keys())))
    result = ctx.execute(
        args,
        environment = dotnet.env,
        quiet = False,
        working_directory = str(fetch_project.dirname),
    )
    if result.return_code != 0:
        fail(result.stdout)

    # first we have to collect all the target framework information for each package
    ctx.report_progress("Processing packages")
    _process_assets_json(ctx, dotnet, config)

    # once we have the full information for each package, we can write the build file for that package
    ctx.report_progress("Generating build files")
    _generate_build_files(ctx, config)

def _pkg_fail(pkg_id, message):
    fail("[{}] ".format(pkg_id), message)

def _compare_versions(a, b):
    length = min(len(a.number_parts), len(b.number_parts))
    for i in range(length):
        diff = a.number_parts[i] - b.number_parts[i]
        if diff != 0:
            return diff

    if len(a.number_parts) == len(b.number_parts):
        if a.suffix != None:
            return 1
        if b.suffix != None:
            return -1
        return 0

    return len(a.number_parts) - len(b.number_parts)

def _semantic_version(version_string):
    version_parts = version_string.split("-")
    number_parts = [int(p) for p in version_parts[0].split(".")]
    suffix = None
    if len(version_parts) > 1:
        suffix = version_parts[1]

    return struct(
        string = version_string,
        number_parts = number_parts,
        suffix = suffix,
    )

def _pkg(name, version, pkg_id = None):
    name_lower = name.lower()
    version_lower = version.lower()
    if pkg_id == None:
        pkg_id = name_lower + "/" + version_lower

    return struct(
        name = name,
        name_lower = name_lower,
        # this field has to be compatible with the NuGetPackageInfo struct
        version = version,
        semver = _semantic_version(version_lower),
        label = "//{}".format(name),
        pkg_id = pkg_id,
        frameworks = {},  # dict[tfm: string] string is a filegroup to be written into a build file
        all_files = [],  # list[str]
        deps = {},  # dict[tfm: list[pkg]]
        filegroups = [],
    )

def _generate_nuget_configs(ctx, config):
    substitutions = prepare_nuget_config(
        config.packages_folder,
        True,
        # todo(#46) allow custom package sources
        {"nuget.org": "https://api.nuget.org/v3/index.json"},
    )
    ctx.template(
        ctx.path(config.fetch_config),
        ctx.attr._config_template,
        substitutions = substitutions,
    )

    substitutions = prepare_nuget_config(
        config.packages_folder,
        False,  # no fetch allowed at build time
        {},  # don't even add sources, just in case
    )
    ctx.template(
        ctx.path(NUGET_BUILD_CONFIG),
        Label("@my_rules_dotnet//dotnet/private/msbuild:NuGet.tpl.config"),
        executable = False,
        substitutions = substitutions,
    )

def _generate_fetch_project(ctx, config):
    build_traversal = MSBuildSdk(name = "Microsoft.Build.Traversal", version = "3.0.3")

    _process_packages(ctx, config)

    project_names = []
    for tfm, pkgs in config.packages_by_tfm.items():
        proj_name = tfm + ".proj"
        project_names.append(proj_name)
        substitutions = prepare_project_file(
            DEFAULT_SDK,
            paths.join(config.intermediate_base, tfm),
            [],
            pkgs.values(),
            config.fetch_config,  # this has to be specified for _every_ project
            tfm,
        )
        ctx.template(
            ctx.path(paths.join(config.fetch_base, proj_name)),
            ctx.attr._tfm_template,
            substitutions = substitutions,
        )

    substitutions = prepare_project_file(
        build_traversal,
        paths.join(config.intermediate_base, "traversal"),
        project_names,
        [],
        config.fetch_config,
        None,  # no tfm for the traversal project
    )
    fetch_project = ctx.path(paths.join(config.fetch_base, "nuget.fetch.proj"))
    ctx.template(
        fetch_project,
        ctx.attr._master_template,
        substitutions = substitutions,
    )
    return fetch_project

def _process_packages(ctx, config):
    seen_names = {}
    for spec, frameworks in ctx.attr.packages.items():
        parts = spec.split(":")
        if len(parts) != 2:
            fail("Invalid version spec, expected `packagename:version-string` got {}".format(spec))

        requested_name = parts[0]
        version_spec = parts[1]

        _record_package(config, seen_names, requested_name, version_spec, frameworks)

    tfms = config.packages_by_tfm.keys()

    pkg_name, version, tfm = ctx.attr.test_logger.split(":")
    _record_package(config, seen_names, pkg_name, version, frameworks)

def _record_package(config, seen_names, requested_name, version_spec, frameworks):
    # todo(#53) don't count on the Version Spec being a precise version
    pkg = _pkg(requested_name, version_spec)

    if pkg.name_lower in seen_names:
        # todo(#47)
        fail("Multiple package versions are not supported.")
    seen_names[pkg.name_lower] = True

    config.packages[pkg.pkg_id] = pkg

    for tfm in frameworks:
        tfm_dict = config.packages_by_tfm.setdefault(tfm, {})
        tfm_dict[pkg.name_lower] = pkg

def _get(obj, name):
    value = obj.get(name, None)
    if value == None:
        fail("Missing required json key in project.assets.json, it is likely corrupted: '{}'".format(name))
    return value

def _get_filegroup(desc, name):
    group = desc.pop(name, None)
    if group != None:
        # todo(#48) figure out what to do with the values of this dictionary
        return group.keys()

    # this will be None if the requested group name isn't present in the NuGet package. i.e. not all packages have the
    #   `resources` group.
    return None

def _process_assets_json(ctx, dotnet, config):
    for tfm, tfm_dict in config.packages_by_tfm.items():
        # reminder: tfm_dict is {pkg_id_lower:struct}
        path = paths.join(config.fetch_base, config.intermediate_base, tfm, "project.assets.json")
        assets = json.decode(ctx.read(path))

        version = _get(assets, "version")
        if version != 3:  # no idea how often this changes
            fail("Unsupported project.assets.json version: {}.".format(version))

        # tfm = netcoreap3.1; tfn = Microsoft.NETCore.App;
        anchor = assets
        for part in ["project", "frameworks", tfm, "frameworkReferences"]:
            anchor = _get(anchor, part)
        tfn = anchor.keys()[0]
        config.tfm_mapping[tfm] = tfn
        overrides = _get_overrides(ctx, dotnet, tfn)

        # dict[pkg_id: Object]
        # sha512
        # type: valid values are [package, project], but package is the only value we will get in this particular usage
        # path: the filepath to the package folder, appears to be pkg_id_lower
        # files: a list of all files contained in the package, the actual .nupkg is not in this list.
        libraries = _get(assets, "libraries")

        # dict[ _tfi_: dict[ filegroup_name: path_dict ]]
        targets = _get(assets, "targets")
        if len(targets) > 1:
            fail("Expected only one Target Framework to be restored in {} but found '{}'".format(
                path,
                "'; '".join(targets.keys()),
            ))

        discovered_deps = {}
        missing_deps = {}
        unused_deps = {}
        tfm_pkgs = targets.values()[0]
        for pkg_id, desc in tfm_pkgs.items():
            pkg_id_lower = pkg_id.lower()

            pkg_type = desc.pop("type", None)
            if pkg_type != "package":
                fail("[{}] Unexpected dependency type {}.".format(pkg_id, pkg_type))

            # todo(#53) support non-precise version specs
            pkg = config.packages.get(pkg_id_lower, None)

            transitive = False
            if pkg == None:
                # nuspec files can specify version specs, not exact versions, so we'll have to index by package name
                # and hope for the best.
                pkg_name, pkg_version = pkg_id.split("/")
                pkg = _pkg(pkg_name, pkg_version, pkg_id_lower)

                if pkg.name_lower in discovered_deps:
                    fail("[{}] Multiple versions of the same package in package deps is not supported. " +
                         "Package name: {}.".format(pkg_id, pkg_name))

                if pkg.name_lower in tfm_dict:
                    # this isn't very accurate since the user can use whatever casing they want in tfm_dict
                    # it will help detect automated names though
                    fail("[{}] Multiple versions of the same package is not supported. " +
                         "Package name: {}.".format(pkg_id, pkg_name))

                transitive = True
                tfm_dict[pkg.name_lower] = pkg
                config.packages[pkg.pkg_id] = pkg

            discovered_deps[pkg.name_lower] = pkg
            expecting = missing_deps.pop(pkg.name_lower, [])
            if len(expecting) > 0:
                for expecting_pkg in expecting:
                    expecting_pkg.deps.setdefault(tfm, []).append(pkg)
            elif transitive:
                unused_deps[pkg.name_lower] = True

            # dict[ CanonicalName: VersionString ]
            pkg_deps = desc.pop("dependencies", None)
            if pkg_deps != None:
                for canonical_name, pkg_version in pkg_deps.items():
                    name_lower = canonical_name.lower()
                    dep = discovered_deps.get(name_lower, None)
                    unused_deps.pop(name_lower, None)
                    if dep == None:
                        missing_deps.setdefault(name_lower, []).append(pkg)
                        continue
                    pkg.deps.setdefault(tfm, []).append(dep)

            # list of all files in the package. NuGet needs these to generate downstream project.assets.json files
            # during restore of projects we are actually compiling.
            pkg_files = _get(libraries, pkg_id)["files"]
            pkg.all_files.extend([
                _package_file_path(config, pkg_id, f)
                for f in pkg_files
            ])
            pkg.all_files.append(
                _package_file_path(config, pkg_id, pkg_id_lower.replace("/", ".") + ".nupkg"),
            )

            if _get_override(overrides, pkg) != None:
                # we don't need any of these files because they will be present in the sdk itself
                continue

            _accumulate_files(pkg.filegroups, config, desc, pkg_id, "compile")
            _accumulate_files(pkg.filegroups, config, desc, pkg_id, "runtime")
            _accumulate_files(pkg.filegroups, config, desc, pkg_id, "build", "buildMultiTargeting")

            _accumulate_resources(pkg, config, desc)

            # resource = _get_filegroup(desc, "resource")  # todo(#48)

            remaining = desc.keys()
            if len(remaining) > 0:
                # todo(#49): decide if we want to do anything here.
                # fail("[{}] Unknown filegroups: {}".format(pkg_id, ", ".join(remaining)))
                pass

        if len(unused_deps) > 0:
            fail("Found unused deps for target framework {}: {}".format(tfm, ", ".join(unused_deps.keys())))
        if len(missing_deps) > 0 or False:
            fail("Found packages expecting deps, but didn't find the dep: {}".format("; ".join([
                "{}: {}".format(pkg_name, ", ".join([expecting_pkg.pkg_id for expecting_pkg in expecting_pkg_list]))
                for pkg_name, expecting_pkg_list in missing_deps.items()
            ])))

        for pkg in tfm_dict.values():
            override = _get_override(overrides, pkg)
            pkg.frameworks[tfm] = _nuget_file_group(tfm, pkg.deps.get(tfm, []), pkg.filegroups, override)

def _package_file_path(config, pkg_id, file_path):
    return "//:" + "/".join([config.packages_folder, pkg_id.lower(), file_path])

def _accumulate_files(filegroups_list, config, desc, pkg_id, *names):
    """Collect a NuGetFileGroup (see definitions).

    Args:
        desc: dict[pkg_path, dict()] so far, for all file groups other than `resource` the bottom dict is empty
            for `resource` it appears to be a list of locales
    Returns a `_nuget_file_list` fragment for substitution into a BUILD file.
    """
    labels = []
    primary_name = names[0]
    for name in names:
        parsed_files = _get_filegroup(desc, name)
        if parsed_files == None:
            continue

        for file in parsed_files:
            if file.endswith("_._"):
                # https://stackoverflow.com/a/36338215/2524934
                # we'll need to include these if we no longer use all_files as an input to the restore target in
                # assembly.bzl, we might do that for #67
                continue

            label = _package_file_path(config, pkg_id, file)
            labels.append(label)
            config.all_files.append(label)

    if len(labels) == 0:
        return
    filegroups_list.append(_nuget_file_list(primary_name, labels))

def _nuget_file_list(name, files):
    return "{name} = [\n        \"{items}\",\n    ],".format(
        name = name,
        items = "\",\n        \"".join(files),
    )

def _accumulate_resources(pkg, config, desc):
    resource = desc.pop("resource", None)
    if resource == None:
        return

    files = {}
    for file, file_desc in resource.items():
        locale = file_desc.pop("locale", None)
        if locale == None:
            _pkg_fail(pkg.pkg_id, "No locale listed for resource file {}".format(file))
        if len(file_desc.keys()) > 0:
            _pkg_fail(pkg.pkg_id, "Unkown metadata for resource file {}: {}".format(file, file_desc.keys()))

        files[_package_file_path(config, pkg.pkg_id, file)] = locale
    pkg.filegroups.append("resource = " + json.encode_indent(files, prefix = "    ", indent = "    "))

# this name is confusing

def _nuget_file_group(name, deps, groups, override):
    deps_string = "\",\n        \"".join([
        dep.label
        for dep in deps
    ])
    format_string = """nuget_filegroup(
    name = "{name}",
    override_version = {override_version},
    deps = [{deps}],
    {items}
)
"""
    return format_string.format(
        name = name,
        override_version = "\"{}\"".format(override.semver.string) if override != None else None,
        deps = "\n        \"{}\",\n    ".format(deps_string) if len(deps) > 0 else "",
        items = "\n    ".join(groups),
    )

def _get_override(overrides, pkg):
    override = overrides.get(pkg.name_lower, None)
    if override != None and _compare_versions(override.semver, pkg.semver) >= 0:
        return override
    return None

def _get_overrides(ctx, dotnet, tfn):
    """Return a dict[ pkg_id, bool] indicating that pkg_id is overridden by the particular framework.

    PackageOverrides.txt contains a list of packages for a framework that override NuGet packages. This means that if
    a particular package is requested, and it is listed in PackageOverrides.txt, then NuGet will not restore that
    package because it is implemented/included by the framework itself.
    """
    base_fail = "Cannot find package overrides for tfn {}: ".format(tfn)
    ref_dir = ctx.path(paths.join(dotnet.sdk_root, "packs", tfn + ".Ref"))
    dirs = ref_dir.readdir()
    if len(dirs) != 1:
        fail(base_fail + "unexpected packs contents: {}".format(tfn, ", ".join(dirs)))

    data_dir = dirs[0].get_child("data")
    if data_dir.exists != True:
        fail(base_fail + "data dir does not exist: {}".format(str(data_dir)))

    overrides_file = data_dir.get_child("PackageOverrides.txt")
    overrides = {}
    if overrides_file.exists != True:
        return overrides

    contents = ctx.read(overrides_file)
    for line in contents.splitlines():
        # line is CanonicalName|version i.e. System.Net.Http|4.3.0
        canonical_name, version = line.split("|")
        pkg_id = canonical_name.lower() + "/" + version.lower()
        pkg = _pkg(canonical_name, version, pkg_id)
        overrides[pkg.name.lower()] = pkg

    return overrides

def _generate_build_files(ctx, config):
    all_all_files = []
    for pkg in config.packages.values():
        # these are labels, and we need them to be paths to be used for exports_files()
        all_all_files.extend([f[3:] for f in pkg.all_files])
        ctx.template(
            ctx.path(paths.join(pkg.name, "BUILD.bazel")),
            ctx.attr._nuget_import_template,
            substitutions = {
                "{name}": pkg.name,
                "{version}": pkg.version,
                "{frameworks}": "\",\n        \"".join([
                    ":" + tfm
                    for tfm in pkg.frameworks.keys()
                ]),
                "{framework_filegroups}": "\n\n".join(pkg.frameworks.values()),
                "{all_files}": "\",\n        \"".join(pkg.all_files),
            },
        )

    test_logger = ctx.attr.test_logger.split(":")[0]
    ctx.template(
        ctx.path("BUILD.bazel"),
        ctx.attr._root_template,
        substitutions = {
            "{test_logger}": test_logger,
            "{file_list}": _json_bzl(all_all_files, ""),
            "{nuget_build_config}": NUGET_BUILD_CONFIG,
            "{tfm_mapping}": _json_bzl(config.tfm_mapping),
        },
    )

def _json_bzl(obj, prefix = "    "):
    return json.encode_indent(obj, prefix = prefix, indent = "    ")

nuget_fetch = repository_rule(
    implementation = _nuget_fetch_impl,
    attrs = {
        "packages": attr.string_list_dict(),
        "test_logger": attr.string(
            default = "JunitXml.TestLogger:3.0.87:netstandard2.0",
        ),
        # todo(#63) link this to the primary nuget folder if it is not the primary nuget folder
        "dotnet_sdk_root": attr.label(
            default = Label("@dotnet_sdk//:ROOT"),
            executable = True,
            cfg = "exec",
        ),
        "use_host": attr.bool(
            default = False,
            doc = ("When false (default) nuget packages will be fetched into a bazel-only directory, when true, the " +
                   "host machine's global packages folder will be used. This is determined by executing " +
                   "`dotnet nuget locals global-packages --list"),
        ),
        "_master_template": attr.label(
            default = Label("@my_rules_dotnet//dotnet/private/msbuild:project.tpl.proj"),
        ),
        "_tfm_template": attr.label(
            default = Label("@my_rules_dotnet//dotnet/private/msbuild:project.tpl.proj"),
        ),
        "_config_template": attr.label(
            default = Label("@my_rules_dotnet//dotnet/private/msbuild:NuGet.tpl.config"),
            allow_single_file = True,
        ),
        "_nuget_import_template": attr.label(
            default = Label("@my_rules_dotnet//dotnet/private/toolchain:BUILD.nuget_import.bazel"),
            allow_single_file = True,
        ),
        "_root_template": attr.label(
            default = Label("@my_rules_dotnet//dotnet/private/toolchain:BUILD.nuget.bazel"),
            allow_single_file = True,
        ),
    },
)
