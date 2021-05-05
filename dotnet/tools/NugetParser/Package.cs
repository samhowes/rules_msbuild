using System.Collections.Generic;

namespace NuGetParser
{
    public class Package
    {
        public string RequestedName { get; }
        public string Version { get; }
        public string Id { get; }
        // todo(#99): append nupkg to this
        public List<string> AllFiles { get; } = new List<string>();
        public Dictionary<string, List<Package>> Deps { get; } = new Dictionary<string, List<Package>>();
        public string Label { get; }
        public string CanonicalId { get; set; }

        public Package(string requestedName, string version)
        {
            RequestedName = requestedName;
            Version = version;
            Id = $"{requestedName}/{version}".ToLower();
            Label = $"@nuget//{requestedName}";
        }
    }
}