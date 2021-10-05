using System;
using System.Collections;
using System.Collections.Generic;

namespace NuGetParser
{
    public class Package
    {
        public static Dictionary<string, Package> PackageDict() => PackageDict<Package>();

        public static Dictionary<string, T> PackageDict<T>() =>
            new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        
        public string RequestedName { get; }
        public string Label { get; }

        public Dictionary<string, PackageVersion> Versions { get; } = new Dictionary<string, PackageVersion>(StringComparer.OrdinalIgnoreCase);

        public Package(string requestedName)
        {
            RequestedName = requestedName;
            Label = $"@nuget//{requestedName}";
        }
    }

    public class PackageVersion
    {
        public PackageVersion(PackageId id)
        {
            Id = id;
        }

        public PackageId Id { get; }
        
        // todo(#99): append nupkg to this
        public List<string> AllFiles { get; set; } = new List<string>();
        public Dictionary<string, List<PackageId>> Deps { get; set; } = new Dictionary<string, List<PackageId>>();
    }

    public class FrameworkDependency
    {
        public Dictionary<string, PackageVersion> Versions { get; }
            = new Dictionary<string, PackageVersion>(StringComparer.OrdinalIgnoreCase);

        public FrameworkDependency(string tfm)
        {
            Tfm = tfm;
        }
        public string Tfm { get; set; }
    }
}