#nullable enable
using System;

namespace NuGetParser
{
    public class PackageId
    {
        public PackageId(string name, string? version)
        {
            if (!string.IsNullOrEmpty(version))
            {
                if (version[0] == '[')
                {
                    var parts = version.Split(",");
                    if (parts.Length > 1)
                    {
                        // "[1.2.3, )"
                        version = parts[0][1..];
                    }
                    else
                    {
                        // "[1.2.3]"
                        version = version[1..^1];
                    }
                }   
            }

            Name = name;
            Version = version ?? "";
            String = $"{name}/{version}";
        }
        public PackageId(string id)
        {
            var parts = id.Split('/');
            Name = parts[0];
            if (parts.Length > 1)
                Version = parts[1];
            else
                Version = "";
            String = id;
        }

        public static implicit operator PackageId(string id)
        {
            return new PackageId(id);
        }

        public static explicit operator string(PackageId id) => id.String;
        
        public string Name { get; set; }
        public string Version { get; }
        public string String { get; set; }

        public override int GetHashCode()
        {
            return Version.GetHashCode(StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(Version, obj);
        }
    }
}