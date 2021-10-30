#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NuGetParser
{
    public class AssetsReader
    {
        private readonly Files _files = null!;
        private readonly string _dotnetRoot = null!;
        private JsonElement _assets;
        private JsonElement _frameworkInfo;
        private JsonElement _libraries;
        private JsonElement _tfmPackages;
        private string _tfm = null!;
        private readonly Dictionary<string,PackageId> _overrides = null!;

        // for testing with moq
        protected AssetsReader() {}
        public AssetsReader(Files files, string dotnetRoot)
        {
            _files = files;
            _dotnetRoot = dotnetRoot;
            _overrides = new Dictionary<string, PackageId>(StringComparer.OrdinalIgnoreCase);
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
            
            if (_frameworkInfo.TryGetProperty("frameworkReferences", out var refs))
            {
                foreach (var @ref in refs.EnumerateObject())
                {
                    if (@ref.Value.TryGetProperty("privateAssets", out var assets))
                    {
                        if (assets.GetString() != "all") continue;

                        var match = Regex.Match(tfm, @"[\d\.]+");
                        if (!match.Success) continue;
                        
                        var tfmVersion = match.Value;
                        
                        var overrides = Path.Combine(
                            _dotnetRoot, "packs",
                            @ref.Name + ".Ref", tfmVersion + ".0",
                            "data", "PackageOverrides.txt");

                        var overridesList = _files.ReadAllLines(overrides)
                            .Select(l => l.Split('|'))
                            .Select(a => new PackageId(a[0], a[1]));
                        foreach (var packageId in overridesList)
                        {
                            _overrides.GetOrAdd(packageId.Name, () => packageId);
                        }
                    }
                }
            }
            
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

                var version = new PackageVersion(id);

                PackageId? overrideId;
                if (_overrides.TryGetValue(id.Name, out overrideId))
                {
                    version.Override = overrideId;
                }

                if (overrideId == null || id.Compare(overrideId) > 0)
                {
                    version.AllFiles = packageDesc.Value.GetProperty("files")
                        .EnumerateArray()
                        .Select(f => f.GetString())
                        .ToList();
                }
                
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