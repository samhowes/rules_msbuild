#nullable enable
using System;
using System.Linq;

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

        private (int[], string?)? _versionParts;

        public int Compare(PackageId other)
        {
            var left = VersionParts;
            var right = other.VersionParts;
            for (int i = 0; i < left.number.Length; i++)
            {
                if (i > right.number.Length - 1) return 1;
                var diff = left.number[i] - right.number[i];
                if (diff == 0) continue;
                return diff;
            }
            // same version, if qualifier is not null, then it is a pre-release
            if (left.qualifier != null && right.qualifier == null) return -1;
            if (right.qualifier != null && left.qualifier == null) return 1;

            // todo: properly compare the qualifier
            return 0; 
        }

        public (int[] number, string? qualifier) VersionParts
        {
            get
            {
                if (_versionParts != null) return _versionParts.Value;
                var numberAndString = Version.Split('-');
                var numbers = numberAndString[0].Split('.').Select(Int32.Parse).ToArray();
                var str = numberAndString.Length > 1 ? numberAndString[1] : null;
                _versionParts = (numbers, str);
                return _versionParts.Value;
            }
        }

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