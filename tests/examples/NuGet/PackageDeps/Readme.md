# //tests/examples/NuGet/PackageDeps

This package tests the building of nuget packages that themselves have dependencies.

The `:PackageDeps` target has a dependency on `@nuget//xunit` which itself has a dependency on
`@nuget//xunit.assert`. This tests makes sure that `xunit.assert.dll` is copied to the output
directory.

`@nuget//xunit` itself is what NuGet calls a "metapackage", and it does not actually have any files
that it copies to the output directory.

> Note: `:PackageDeps` **must** be a binary (OutputType = Exe), otherwise no files are copied to the
> output directory by MsBuild.

The `:PacakgeDeps` binary is executed, and output is expected from it because this makes sure that
dotnet can find the dlls using the deps.json file and makes sure that the deps.json file isn't being
used to refer to something in a sandbox somewhere.
