load("@bazel_tools//tools/build_defs/repo:utils.bzl", "update_attrs")
load("//dotnet/private:platforms.bzl", "generate_toolchain_names")
load("//dotnet/private/msbuild:nuget.bzl", "NUGET_BUILD_CONFIG", "prepare_nuget_config")
load("//dotnet/private/toolchain:common.bzl", "default_tfm", "detect_host_platform")
load("//deps:public_nuget.bzl", "PACKAGES")

SDK_NAME = "dotnet_sdk"

_download_sdk_attrs = {
    "version": attr.string(),
    "nuget_repo": attr.string(mandatory = True),
    "shas": attr.string_dict(),
}

def msbuild_register_toolchains(version = None, shas = {}, nuget_repo = "nuget"):
    if not version:
        fail('msbuild_register_toolchains: version must be a string like "3.1.100" or "host"')

    if version == "host":
        _dotnet_host_sdk(name = SDK_NAME, nuget_repo = nuget_repo)
    else:
        if version[0] != "6":
            fail("cannot use version %s; dotnet 6 is required for rules_msbuild" % version)

        _dotnet_download_sdk(
            name = SDK_NAME,
            version = version,
            shas = shas,
            nuget_repo = nuget_repo,
        )
    _register_toolchains(SDK_NAME)

def _dotnet_host_sdk_impl(ctx):
    os, _ = detect_host_platform(ctx)
    dotnet_name = "dotnet" + (".exe" if os == "windows" else "")
    dotnet_path = ctx.which(dotnet_name)
    if dotnet_path == None:
        fail("could not find {} on path".format(dotnet_name))

    version = _try_execute(ctx, [dotnet_path, "--version"]).strip()
    if version[0] != "6":
        fail("cannot use dotnet version %s; dotnet 6 is required for rules_msbuild. Download and install from https://dotnet.microsoft.com/download" % version)

    sdk_list = _try_execute(ctx, [dotnet_path, "--list-sdks"]).split("\n")
    if len(sdk_list) == 0:
        fail("no dotnet sdks are installed")

    sdk_path = None
    for line in sdk_list:
        # example output: `5.0.202 [/usr/local/share/dotnet/sdk]`
        parts = line.split(" ", 1)
        sdk_version = parts[0]
        if sdk_version != version:
            continue
        sdk_path = ctx.path(parts[1][1:-1])
        break

    if sdk_path == None:
        fail("could not find {} in sdk list:\n{}".format(version, sdk_list))

    # sdk_path points to something like `/usr/local/share/dotnet/sdk` which contains a subfolders of all sdk versions
    # i.e. readdir of sdk_path will return: `2.0.3, 2.1.504, ... 5.0.2020`
    # the parent dir is the location of the dotnet exe and contains templates packs host and such
    dotnet_root = sdk_path.dirname
    repo_root = ctx.path("")
    for p in dotnet_root.readdir():
        if p.basename == "sdk":
            ctx.symlink(p.get_child(version).realpath, "sdk/" + version)
            continue
        ctx.symlink(p.realpath, p.basename)
    _sdk_build_file(ctx, version)

def _try_execute(ctx, args):
    res = ctx.execute(args)
    if res.return_code != 0:
        fail("error {} executing `{}`: {}".format(res.return_code, " ".join(args), res.stderr))
    return res.stdout

def _dotnet_download_sdk_impl(ctx):
    version = ctx.attr.version
    os, arch = detect_host_platform(ctx)

    platform = os + "_" + arch
    shas = getattr(ctx.attr, "shas", {})
    sdk_sha = ""
    if platform in shas:
        sdk_sha = shas[platform]

    install_script = None
    script_url = None
    args = None
    sha = ""
    if os == "windows":
        script_url = "https://dot.net/v1/dotnet-install.ps1"
        install_script = ctx.path("dotnet-install.ps1")
        args = ["powershell", "-NoProfile", str(install_script)]
    else:
        script_url = "https://dot.net/v1/dotnet-install.sh"
        install_script = ctx.path("dotnet_install.sh")
        #sha = "575aaa47b0e2ed6f64e3f76d42386656e4efe56c018d3245d11d51dc7ed1b983"
        args = [str(install_script)]

    ctx.download(
        script_url,
        install_script,
        sha256 = sha,
        executable = True,
    )

    # bash supports the same as the powershell script, so we can use the same set of args
    args.extend(["-DryRun", "-NoPath", "-Version", version])

    res = ctx.execute(args)

    url = None
    for line in res.stdout.split("\n"):
        if "rimary" in line and ("url" in line or "URL" in line) and "-dev-" not in line:
            url = line.rsplit(" ", 1)[1]
            break

    if url == None:
        fail("failed to parse Primary url from:\nstdout:{}\nstderr:{}".format(res.stdout, res.stderr))

    ctx.report_progress("Downloading Dotnet Sdk from {}".format(url))

    res = ctx.download_and_extract(url, sha256 = sdk_sha)

    attr_udpates = {}
    if sdk_sha == "":
        shas = getattr(ctx.attr, "shas", {})
        orig = dict(shas.items())
        orig[platform] = res.sha256
        attr_udpates["shas"] = orig

    _sdk_build_file(ctx, ctx.attr.version)

    return update_attrs(ctx.attr, _download_sdk_attrs, attr_udpates)

_dotnet_host_sdk = repository_rule(
    implementation = _dotnet_host_sdk_impl,
    attrs = {
        "nuget_repo": attr.string(mandatory = True),
    },
)

_dotnet_download_sdk = repository_rule(
    implementation = _dotnet_download_sdk_impl,
    attrs = _download_sdk_attrs,
)

def _sdk_build_file(ctx, version):
    """Creates the BUILD file for the downloaded dotnet sdk

    Assumes there is only one SDK in this directory, this is accurate for an
    individual directory, but dotnet is structured to allow multiple sdk versions to
    exist nicely next to each other.
    """
    root = ctx.file("ROOT")

    ctx.template("AlternateCommonProps.props", Label("//dotnet/private/msbuild:AlternateCommonProps.props"), executable = False)
    ctx.template("Directory.Bazel.props", Label("//dotnet/private/msbuild:Directory.Bazel.props"), executable = False)

    dynamics = []

    # create dotnet init files so dotnet doesn't noisily print them out on the first build
    init_files = [
        ".dotnet/{}.{}".format(version, f)
        for f in [
            "aspNetCertificateSentinel",
            "dotnetFirstUseSentinel",
            "toolpath.sentinel",
        ]
    ]

    for f in init_files:
        ctx.file(f, "")

    os, arch = detect_host_platform(ctx)
    if os == "windows":
        # only on windows, on non-windows, there won't be an exe extension and it registers as a self edge
        # dependency loop
        dynamics.append("""filegroup(
   name = "dotnet",
   srcs = ["dotnet.exe"],
)""")

    deps_dict = {}
    for k in PACKAGES.keys():
        parts = k.split("/")
        deps_dict["@{}//{}".format(ctx.attr.nuget_repo, parts[0])] = True
    builder_deps = [k for k in deps_dict.keys()]

    builder_tfm = default_tfm(version)
    ctx.template(
        "BUILD.bazel",
        Label("@rules_msbuild//dotnet/private/toolchain:BUILD.sdk.bazel"),
        executable = False,
        substitutions = {
            "{dotnetos}": os,
            "{dotnetarch}": arch,
            "{exe}": ".exe" if os == "windows" else "",
            "{version}": version,
            "{major_version}": version.split(".")[0],
            # sdk deps
            "{dynamics}": "\n".join(dynamics),

            # dotnet_config
            # assumes this will be put in <output_base>/external/<sdk_name>
            "{trim_path}": str(ctx.path("").dirname.dirname),

            # the sdk has an execution time dependency on the primary nuget repo
            # all nuget repos have a loading time dependency on the dotnet binary that is downloaded with the sdk.
            # It's almost a circular dependency, but not quite.
            "{nuget_config}": "@{}//:{}".format(ctx.attr.nuget_repo, NUGET_BUILD_CONFIG),
            "{tfm_mapping}": "@{}//:tfm_mapping".format(ctx.attr.nuget_repo),
            "{nuget_repo}": ctx.attr.nuget_repo,
            "{builder_deps}": "\",\n        \"".join(builder_deps),
            "{builder_tfm}": builder_tfm,
        },
    )

def _register_toolchains(repo):
    labels = [
        "@{}//:{}".format(repo, name)
        for name in generate_toolchain_names()
    ]
    print(labels)
    native.register_toolchains(*labels)

def _parse_version(version):
    """Parses a version string like "3.1" and returns a tuple of numbers or None"""
    l, r = 0, 0
    parsed = []
    for c in version.elems():
        if c == ".":
            if l == r:
                # empty component
                return None
            parsed.append(int(version[l:r]))
            r += 1
            l = r
            continue

        if c.isdigit():
            r += 1
            continue

        # pre-release suffix
        break

    if l == r:
        # empty component
        return None
    parsed.append(int(version[l:r]))
    if len(parsed) == 2:
        # first minor version, like (1, 15)
        parsed.append(0)
    if len(parsed) != 3:
        # too many or too few components
        return None
    if r < len(version):
        # pre-release suffix
        parsed.append(version[r:])
    return tuple(parsed)
