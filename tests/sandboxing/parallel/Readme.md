# //tests/sandboxing/parallel

This package is to demonstrate [#33](https://github.com/samhowes/my_rules_dotnet/issues/33). The
goal is to get bazel to invoke a dotnet\_\* target for two different configurations at the same
time. MsBuild uses "Shared Compilation" which apparently starts a central MsBuild server, and if an
instance of MsBuild detects that the server is compiling its output already, then it will simply
tell the existing server to write its output to the current instance's directory.

Whatever the mechanism that actually executes, sandboxing on a linux ubuntu vm will put each process
in a read-only file system, so the compilation result cannot be output correctly.

run: `bazel build //tests/sandboxing/parallel`

**DO NOT** build the targets individually for a proper test.

To test parallel execution on your machine, do NOT execute the other targets individually as that
will prevent the actions from running in parallel.
