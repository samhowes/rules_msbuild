using System;
using System.Collections;
using System.Collections.Generic;

namespace NuGetParser
{
    public class Package
    {
        public string RequestedName { get; }
        public string Label { get; }
        public string CanonicalName { get; set; }

        public Dictionary<string, FrameworkDependency> Frameworks { get; } =
            new Dictionary<string, FrameworkDependency>();

        public Dictionary<string, PackageVersion> Versions { get; } = new Dictionary<string, PackageVersion>(StringComparer.OrdinalIgnoreCase);

        public Package(string requestedName)
        {
            RequestedName = requestedName;
            Label = $"@nuget//{requestedName}";
        }
    }

    public class PackageVersion
    {
        public PackageVersion(Package pkg, string version)
        {
            String = version;
            Id = $"{pkg.RequestedName}/{version}".ToLower();
            Label = pkg.Label + ":" + version;
        }

        public string Label { get; }

        public string String { get; }
        public string Id { get; }
        // todo(#99): append nupkg to this
        public List<string> AllFiles { get; } = new List<string>();
        public string CanonicalId { get; set; }
    }

    public class FrameworkDependency
    {
        public string Version { get; }

        public FrameworkDependency(string tfm, string version)
        {
            Tfm = tfm;
            Version = version;
        }
        public string Tfm { get; set; }
        public List<Package> Deps { get; } = new List<Package>();
    }
}