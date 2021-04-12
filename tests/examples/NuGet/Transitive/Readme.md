# //tests/examples/NuGet/Transitive

The targets in this package test that NuGet packages are transitively copied correctly. The Binary
target depends on the Library Target, and the Library target depends on `@nuget//newtonsoft.json`.
The Binary sends a json string to the Library, that then parses it using Newtonsoft.Json, and
returns the resulting object. The `foo` property of that object is then written out to the console.

Proper compilation of `Binary` means that `newtonsoft.json.dll` is copied to the output directory,
and the text `bar` is written to the console.

Proper compilation of `Library` means that `newtonsoft.json.dll` is **not** copied to its output
directory, and a deps.json file is created that indicates that `Newtonsoft.Json/<version>` is a
dependency of `Library`.

In this test, the contents of the deps file is not verified directly, but indirectly via making sure
that the `Binary` test passes. The `Library` test asserts that the dll is **not** created.
