# NuGet

## Unique Challenges

In a typical MsBuild solution file, any given csproj file can target any given framework version. It is completely valid to have one project target netcoreapp3.1 and another project target netcoreapp2.2. The same project can even target both netcoreapp3.1 and netcoreapp2.2 simultaneously with the `TargetFrameworks` Property.

A given project can target any framework at or below the level of the Sdk installed. i.e. if you have SDK 3.1.100 installed, you can target <= netcoreapp3.1 and if you have SDK 5.0.0 installed, you can target <= 5.0.0. If you are targeting the version of the SDK installed, i.e netcoreapp3.1 when 3.1.100 is installed, then no NuGet packages are necessary. However, if SDK 3.1.100 is installed and netcoreapp2.2 is targeted, then Microsoft.NETCore.App version 2.2.0 is implicity added as a package restore via the `DefaultImplicitPackages` property of the csproj.

A given package then has N versions, and for each version V of N, M frameworks can be supported by that package. For each framework F of M, a distinct list of dependencies can be listed in the NuSpec file. Therefore a unique dependency graph exists for each combination of V,F.

An additional complexity is that a NuSpec need not list higher framework versions that it supports, as newer frameworks are backwards compatible.

All of these things make sense given the capabilities that Dotnet provides, however it means that some logic and knowledge of the package system is necessary in order to do proper dependency management. A given bazel target can't simply declare a dependency on Package P, version V it has to declare a dependency on Framework F, Package P, Version V, and those declarations have to be fed to NuGet so the correct packages are downloaded. However, if only one target T1 declares a dependency on Framework F1, of 100 targets targeting Framework F2 with 100 Packages referenced in common, only the packages requested by target T1 need to be fetched for F1. We need to avoid fetching the dependency graph of F1 for all 100 packages, and instead only fetch the dependency graph of F1 for the packages that T1 references.

In addition, a given package P can exclude a particular folder of dependency A, and Package Q can require that folder.

Newtonsoft.Json.nuspec

```xml
<dependencies>
    <group targetFramework=".NETFramework2.0" />
    <group targetFramework=".NETFramework3.5" />
    <group targetFramework=".NETFramework4.0" />
    <group targetFramework=".NETFramework4.5" />
    <group targetFramework=".NETPortable0.0-Profile259" />
    <group targetFramework=".NETPortable0.0-Profile328" />
    <group targetFramework=".NETStandard1.0">
        <dependency id="Microsoft.CSharp" version="4.3.0" exclude="Build,Analyzers" />
        <dependency id="NETStandard.Library" version="1.6.1" exclude="Build,Analyzers" />
        <dependency id="System.ComponentModel.TypeConverter" version="4.3.0" exclude="Build,Analyzers" />
        <dependency id="System.Runtime.Serialization.Primitives" version="4.3.0" exclude="Build,Analyzers" />
    </group>
    <group targetFramework=".NETStandard1.3">
        <dependency id="Microsoft.CSharp" version="4.3.0" exclude="Build,Analyzers" />
        <dependency id="NETStandard.Library" version="1.6.1" exclude="Build,Analyzers" />
        <dependency id="System.ComponentModel.TypeConverter" version="4.3.0" exclude="Build,Analyzers" />
        <dependency id="System.Runtime.Serialization.Formatters" version="4.3.0" exclude="Build,Analyzers" />
        <dependency id="System.Runtime.Serialization.Primitives" version="4.3.0" exclude="Build,Analyzers" />
        <dependency id="System.Xml.XmlDocument" version="4.3.0" exclude="Build,Analyzers" />
    </group>
    <group targetFramework=".NETStandard2.0" />
</dependencies>
```

The dependency on (Framework, Package, Version) lends nicely to managing packages at the project level, and not the solution level.

Bazel encourages [managing packages at the repository level](https://docs.bazel.build/versions/master/external.html#shadowing-dependencies) and [not using versions in target names](https://docs.bazel.build/versions/4.0.0/best-practices.html#versioning).
