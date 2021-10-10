#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NuGetParser
{
    public class PackageId
    {
        public PackageId(string name, string version)
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
            
            Name = name;
            Version = version;
            String = $"{name}/{version}";
        }
        public PackageId(string id)
        {
            var parts = id.Split('/');
            Name = parts[0];
            Version = parts[1];
            String = id;
        }

        public static implicit operator PackageId(string id)
        {
            return new PackageId(id);
        }

        public static explicit operator string(PackageId id) => id.String;
        
        public string Name { get; set; } = null!;
        public string Version { get; } = null!;
        public string String { get; set; } = null!;

        public override int GetHashCode()
        {
            return Version.GetHashCode(StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(Version, obj);
        }
    }
    
    public class AssetsReader
    {
        private readonly Files _files = null!;
        private JsonElement _assets;
        private JsonElement _frameworkInfo;
        private JsonElement _libraries;
        private JsonElement _tfmPackages;
        private string _tfm = null!;

        // for testing with moq
        protected AssetsReader() {}
        public AssetsReader(Files files)
        {
            _files = files;
        }

        public virtual string? Init(string objDirectory, string tfm)
        {
            _tfm = tfm;
            _assets = JsonSerializer.Deserialize<JsonElement>(
                _files.GetContents(Path.Combine(objDirectory, "project.assets.json")));
            
            if (!_assets.GetRequired("version", out var version))
            {
                return "missing version";
            }

            if (version.GetInt32() != 3)
            {
                return $"Unsupported project.assets.json version {version.GetInt32()}";
            }

            _frameworkInfo = _assets;
            foreach (var part in new[] {"project", "frameworks", tfm})
            {
                _frameworkInfo.GetRequired(part, out _frameworkInfo);
            }
            
            _tfmPackages = _assets.GetProperty("targets").EnumerateObject().Single().Value;
            _assets.GetRequired("libraries", out _libraries);
            
            return null;
        }

        public virtual string? GetTfn()
        {
            if (_frameworkInfo.TryGetProperty("frameworkReferences", out var refs))
            {
                // "frameworkReferences": {"Microsoft.NETCore.App":{}}
                var tfn = refs.EnumerateObject().ToList().Single().Name;
                return tfn;
            }

            return null;
        }

        public virtual IEnumerable<PackageVersion> GetPackages()
        {
            foreach (var packageDesc in _libraries.EnumerateObject())
            {
                var id = new PackageId(packageDesc.Name);

                var version = new PackageVersion(id)
                {
                    AllFiles = packageDesc.Value.GetProperty("files")
                        .EnumerateArray()
                        .Select(f => f.GetString())
                        .ToList()
                };
                
                var meta = _tfmPackages.GetProperty(id.String);
                if (meta.TryGetProperty("dependencies", out var deps))
                {
                    version.Deps[_tfm] = deps.EnumerateObject()
                        .Select(p => new PackageId(p.Name, p.Value.GetString()))
                        .ToList();    
                }
                else
                {
                    version.Deps[_tfm] = new List<PackageId>();
                }
                
                
                yield return version;
            }
        }
        
        public virtual IEnumerable<PackageId> GetImplicitDependencies()
        {
            string? GetVersionString(JsonElement dep)
            {
                // "version": "[2.0.3, )"
                var versionSpec = dep.GetProperty("version").GetString();
                var versionString = versionSpec.Split(",")[0][1..];
                return versionString;
            }

            if (_frameworkInfo.TryGetProperty("dependencies", out var deps))
            {
                foreach (var dep in deps.EnumerateObject())
                {
                    var versionString = GetVersionString(dep.Value);
                    var imp = dep.Value.TryGetProperty("autoReferenced", out var auto) && auto.GetBoolean();
                    if (!imp) continue;
                    
                    yield return new PackageId(dep.Name, versionString!);
                }
            }

            if (_frameworkInfo.TryGetProperty("downloadDependencies", out deps))
            {
                foreach (var dep in deps.EnumerateArray())
                {
                    var name = dep.GetProperty("name").GetString();
                    var versionString = GetVersionString(dep);
                    yield return new PackageId(name, versionString!);
                }
            }
        }
    }
}