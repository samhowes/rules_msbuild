def dotnetos_to_exe_extension(dotnetos):
    if dotnetos == "windows":
        return ".exe"
    return ""

def dotnetos_to_library_extension(dotnetos):
    return {
        "windows": ".dll",
        "darwin": ".dylib",
    }.get(dotnetos, ".so")
