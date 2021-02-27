
def dotnetos_to_extension(dotnetos):
    if dotnetos == "windows":
        return ".exe"
    return ""


def dotnetos_to_shared_extension(dotnetos):
    return {
        "windows": ".dll",
        "darwin": ".dylib",
    }.get(dotnetos, ".so")
