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
        executable = dotnet.actions.declare_file(dotnet._ctx.label.name + extension)
    #todo specify these
    # runfiles = dotnet._ctx.runfiles(files = [])
    print(dotnet._ctx.files.srcs)
    dotnet.actions.do_nothing(mnemonic='DotnetBuild',inputs= dotnet._ctx.files.srcs)
    dotnet.actions.write(
        output = executable,
        content = "Hello\n",
    )

    return executable#, runfiles
