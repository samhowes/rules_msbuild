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

The dependency on (Framework, Package, Verion) lends nicely to managing packages at the project level, and not the solution level.

Bazel encourages [managing packages at the repository level](https://docs.bazel.build/versions/master/external.html#shadowing-dependencies) and [not using versions in target names](https://docs.bazel.build/versions/4.0.0/best-practices.html#versioning).

The strategy my_rules_dotnet will attempt to take is naming the lastest version of a package `package` and previous versions of a package `package-v1`.

# Modes of NuGet Operation

## 1. Locked Restore

The ideal case:

1. Packages are listed
1. Frameworks depending on those packages are known and listed
1. Transitive closure has been precomputed
1. Complete BUILD files can be generated

## 2. Bootstrapping

First run of the repository or Reinitialization

1. Packages are listed
1. Frameworks depending on those packages are unknown and not listed
1. Transitive closure cannot be computed
1. Fake build files can be generated

Goals:

1. Single source of truth for Target Framework
   1. Allow the dependent target to specify the Target Framework
   1. Do not require that the TargetFramework be listed by the user next to the package
1. Easy to change the Target Framework at any time
1. Allow easy bootstrapping of a [Locked Restore](#1-locked-restore)

Algorithm:

1. Generate faux targets that don't actually build
1. Bazel is now in a valid state for analysis
1. Fail the loading phase: packages can't be retrieved without knowledge of Frameworks dependent on those packages
1. Instruct the user to execute @nuget//:bootstrap
1. @nuget//:bootstrap
   1. Depends on a Bazel query that discovers target frameworks depending on Packages
   1. Outputs a listing packages by framework requesting that package
1. An [Unlocked Restore](#3-unlocked-restore) can now occur

## 3. Unlocked Restore

Bootstrapping has occurred:

1. Packages are listed
1. Frameworks depending on those packages are listed
1. Transitive closure must be computed
1. Complete BUILD files can be generated

# Bootstrap process

```bash
bazel build //... --build_tag_filters "nuget_restore"
bazel query rdeps | bazel run @nuget//:bootstrap
bazel build //...
```
