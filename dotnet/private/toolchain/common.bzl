def detect_host_platform(ctx):
    """Detects a host os and architecture from a repository_ctx."""
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
