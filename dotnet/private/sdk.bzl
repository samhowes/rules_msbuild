load(
    "//dotnet/private:platforms.bzl",
    "generate_toolchain_names",
)
load(
    "//dotnet/private:sdk_urls.bzl",
    "DOTNET_SDK_URLS",
)

def dotnet_register_toolchains(version = None):
    """See /dotnet/toolchains.md#dotnet-register-toolchains for full documentation."""
    sdk_kinds = ("_dotnet_download_sdk")
    existing_rules = native.existing_rules()
    sdk_rules = [r for r in existing_rules.values() if r["kind"] in sdk_kinds]
    if len(sdk_rules) == 0 and "dotnet_sdk" in existing_rules:
        # may be local_repository in bazel_tests.
        sdk_rules.append(existing_rules["dotnet_sdk"])  #todo remove this?

    if version and len(sdk_rules) > 0:
        fail("dotnet_register_toolchains: version set after go sdk rule declared ({})".format(", ".join([r["name"] for r in sdk_rules])))
    if len(sdk_rules) == 0:
        if not version:
            fail('dotnet_register_toolchains: version must be a string like "3.1.100"')  # todo add "or host"
            # elif version == "host":
            #     go_host_sdk(name = "go_sdk")

        else:
            pv = _parse_version(version)
            if not pv:
                fail('dotnet_register_toolchains: version must be a string like "3.1.100" or "host"')  # todo add "or host"

            # if _version_less(pv, MIN_SUPPORTED_VERSION):
            #     print("DEPRECATED: Go versions before {} are not supported and may not work".format(_version_string(MIN_SUPPORTED_VERSION)))
            dotnet_download_sdk(
                name = "dotnet_sdk",
                version = version,
            )

def dotnet_download_sdk(name, **kwargs):
    _dotnet_download_sdk(name = name, **kwargs)
    _register_toolchains(name)

def _dotnet_download_sdk_impl(ctx):
    if not ctx.attr.dotnetos and not ctx.attr.dotnetarch:
        dotnetos, dotnetarch = _detect_host_platform(ctx)
    else:
        if not ctx.attr.dotnetos:
            fail("dotnetarch set but dotnetos not set")
        if not ctx.attr.dotnetarch:
            fail("dotnetos set but dotnetarch not set")
        dotnetos, dotnetarch = ctx.attr.dotnetos, ctx.attr.dotnetarch
    platform = dotnetos + "_" + dotnetarch

    version = ctx.attr.version
    sdks = ctx.attr.sdks

    if not sdks:
        sdks = DOTNET_SDK_URLS[version]

    #     # If sdks was unspecified, download a full list of files.
    #     # If version was unspecified, pick the latest version.
    #     # Even if version was specified, we need to download the file list
    #     # to find the SHA-256 sum. If we don't have it, Bazel won't cache
    #     # the downloaded archive.
    #     if not version:
    #         ctx.report_progress("Finding latest Go version")
    #     else:
    #         ctx.report_progress("Finding Go SHA-256 sums")
    #     ctx.download(
    #         url = [
    #             "https://golang.org/dl/?mode=json&include=all",
    #             "https://golang.google.cn/dl/?mode=json&include=all",
    #         ],
    #         output = "versions.json",
    #     )

    #     data = ctx.read("versions.json")
    #     sdks_by_version = _parse_versions_json(data)

    #     if not version:
    #         highest_version = None
    #         for v in sdks_by_version.keys():
    #             pv = _parse_version(v)
    #             if not pv or _version_is_prerelease(pv):
    #                 # skip parse errors and pre-release versions
    #                 continue
    #             if not highest_version or _version_less(highest_version, pv):
    #                 highest_version = pv
    #         if not highest_version:
    #             fail("did not find any Go versions in https://golang.org/dl/?mode=json")
    #         version = _version_string(highest_version)
    #     if version not in sdks_by_version:
    #         fail("did not find version {} in https://golang.org/dl/?mode=json".format(version))
    #     sdks = sdks_by_version[version]

    if platform not in sdks:
        fail("unsupported platform {}".format(platform))
    filename, sha256 = sdks[platform]
    _remote_sdk(ctx, [filename], ctx.attr.strip_prefix, sha256)

    _sdk_build_file(ctx, platform)

    # create dotnet init files so dotnet doesn't noisily print them out on the first build

    init_files = [
        ctx.file(".dotnet/{}.{}".format(version, f))
        for f in [
            "aspNetCertificateSentinel",
            "dotnetFirstUseSentinel",
            "toolpath.sentinel",
        ]
    ]

_dotnet_download_sdk = repository_rule(
    implementation = _dotnet_download_sdk_impl,
    attrs = {
        "dotnetos": attr.string(),
        "dotnetarch": attr.string(),
        "sdks": attr.string_list_dict(),
        "urls": attr.string_list(default = ["https://dl.google.com/go/{}"]),  # todo fis this url for dotnet
        "version": attr.string(),
        "strip_prefix": attr.string(default = ""),
    },
)

def _remote_sdk(ctx, urls, strip_prefix, sha256):
    if len(urls) == 0:
        fail("no urls specified")
    ctx.report_progress("Downloading and extracting Dotnet toolchain")
    ctx.download_and_extract(
        url = urls,
        stripPrefix = strip_prefix,
        sha256 = sha256,
    )

def _sdk_build_file(ctx, platform):
    """Creates the BUILD file for the downloaded dotnet sdk

    Assumes there is only one SDK in this directory, this is accurate for an
    individual directory, but dotnet is structured to allow multiple sdk versions to
    exist nicely next to each other.
    """
    ctx.file("ROOT")
    dotnetos, _, dotnetarch = platform.partition("_")

    dynamics = []
    pack_labels = []
    packs = ctx.path("packs")
    for p in packs.readdir():
        pack_name = p.basename
        pack_labels.append("\":{}\"".format(pack_name))
        dynamics.append("""
filegroup(
    name = "{pack}",
    srcs = glob(["packs/{pack}/**/*"]),
)""".format(pack = pack_name))

    ctx.template(
        "BUILD.bazel",
        Label("@my_rules_dotnet//dotnet/private:BUILD.sdk.bazel"),
        executable = False,
        substitutions = {
            "{dotnetos}": dotnetos,
            "{dotnetarch}": dotnetarch,
            "{exe}": ".exe" if dotnetos == "windows" else "",
            "{version}": ctx.attr.version,
            "{pack_labels}": ",\n        ".join(pack_labels),
            "{dynamics}": "\n".join(dynamics),
        },
    )

    ctx.template(
        ".nuget/NuGet.Build.config",
        Label("@my_rules_dotnet//dotnet/private:NuGet.Build.config"),
        executable = False,
        substitutions = {
            "{package_repository}": ".nuget",  # no restore allowed for the Build Config
        },
    )

def _detect_host_platform(ctx):
    if ctx.os.name == "linux":
        # untested
        dotnetos, dotnetarch = "linux", "amd64"
        res = ctx.execute(["uname", "-p"])
        if res.return_code == 0:
            uname = res.stdout.strip()
            if uname == "s390x":
                dotnetarch = "s390x"
            elif uname == "i686":
                dotnetarch = "386"

        # uname -p is not working on Aarch64 boards
        # or for ppc64le on some distros
        res = ctx.execute(["uname", "-m"])
        if res.return_code == 0:
            uname = res.stdout.strip()
            if uname == "aarch64":
                dotnetarch = "arm64"
            elif uname == "armv6l":
                dotnetarch = "arm"
            elif uname == "armv7l":
                dotnetarch = "arm"
            elif uname == "ppc64le":
                dotnetarch = "ppc64le"

        # Default to amd64 when uname doesn't return a known value.

    elif ctx.os.name == "mac os x":
        #untested
        dotnetos, dotnetarch = "darwin", "amd64"

        res = ctx.execute(["uname", "-m"])
        if res.return_code == 0:
            uname = res.stdout.strip()
            if uname == "arm64":
                dotnetarch = "arm64"

        # Default to amd64 when uname doesn't return a known value.

    elif ctx.os.name.startswith("windows"):
        dotnetos, dotnetarch = "windows", "amd64"
    elif ctx.os.name == "freebsd":
        #untested
        dotnetos, dotnetarch = "freebsd", "amd64"
    else:
        fail("Unsupported operating system: " + ctx.os.name)

    return dotnetos, dotnetarch

def _register_toolchains(repo):
    labels = [
        "@{}//:{}".format(repo, name)
        for name in generate_toolchain_names()
    ]
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
