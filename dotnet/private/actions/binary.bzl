"""See dotnet/toolchains.md#binary for full documentation."""

def emit_binary(
        dotnet,
        name = "",
        source = None,
        test_archives = [],
        version_file = None,
        info_file = None,
        executable = None):
    """See dotnet/toolchains.md#binary for full documentation."""

    if name == "" and executable == None:
        fail("either name or executable must be set")

    if not executable:
        extension = dotnet.exe_extension
        executable = dotnet.declare_file(dotnet, path = name, ext = extension)
    #todo specify these
    runfiles = dotnet._ctx.runfiles(files = [])

    return executable, runfiles
