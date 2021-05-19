using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using static MyRulesDotnet.Tools.NuGetParser.BazelLogger;
using static NuGetParser.Package;

namespace NuGetParser
{
    public class TfmParser
    {
        public readonly string Tfm;
        private readonly Parser _parent;
        private readonly Dictionary<string, PackageVersion> _unusedDeps;
        private readonly Dictionary<string, List<FrameworkDependency>> _missingDeps;
        private readonly Dictionary<string, Package> _tfmPackages = PackageDict();
        private JsonElement _assets;

        public TfmParser(string tfm, Parser parent)
        {
            Tfm = tfm;
            _parent = parent;
            _unusedDeps = PackageDict<PackageVersion>();
            _missingDeps = PackageDict<List<FrameworkDependency>>();
        }

        public bool LoadRequestedPackages(string projectPath)
        {
            var project = XDocument.Load(projectPath);
            foreach (var reference in project.Descendants("PackageReference"))
            {
                var packageName = reference.Attribute("Include")!.Value;
                var versionString = reference.Attribute("Version")!.Value;
                AddPackage(packageName, versionString);
            }

            return true;
        }
        
        public bool ProcessPackages()
        {
            _assets =
                JsonSerializer.Deserialize<JsonElement>(
                    File.ReadAllText(Path.Combine(_parent.IntermediateBase, Tfm,
                        "project.assets.json")));

            if (!_assets.GetRequired("version", out var version)) return false;

            if (version.GetInt32() != 3)
            {
                Console.WriteLine($"Unsupported project.assets.json version {version.GetInt32()}");
                return false;
            }

            if (!RecordTfmInfo(_assets)) return false;

            if (!ProcessAssets(_assets)) return false;
            return true;
        }

        private bool ProcessAssets(JsonElement assets)
        {
            var tfmPackagesJson = assets.GetProperty("targets").EnumerateObject().Single().Value;
            if (!assets.GetRequired("libraries", out var libraries)) return false;

            Debug(Tfm);
            foreach (var desc in tfmPackagesJson.EnumerateObject())
            {
                if (!Parse(desc, libraries)) return false;
            }

            if (_unusedDeps.Any())
            {
                Console.WriteLine(
                    $"Found unused deps for target framework {Tfm}: {string.Join(";", _unusedDeps.Keys)}");
                return false;
            }

            if (_missingDeps.Any())
            {
                foreach (var (id, expecting) in _missingDeps)
                {
                    if (_tfmPackages.TryGetValue(id.Split("/")[0], out var versionUpgrade))
                    {
                        foreach (var e in expecting)
                        {
                            e.Deps.Add(versionUpgrade);
                        }

                        _missingDeps.Remove(id);
                    }
                }

                if (_missingDeps.Any())
                {
                    Console.WriteLine($"Found packages expecting dependencies, but didn't find the dependencies:");
                    foreach (var (id, expecting) in _missingDeps)
                    {
                        Console.WriteLine($"{id} <= {string.Join(", ", expecting.Select(e => e.Tfm))}");
                    }

                    return false;
                }
            }

            return true;
        }

        public bool Parse(JsonProperty desc, JsonElement libraries)
        {
            var canonicalId = desc.Name;
            
            var type = desc.Value.GetProperty("type");
            if (type.GetString() != "package")
            {
                Console.WriteLine($"Unexpected dependency type: {type}");
                return false;
            }

            var parts = canonicalId.Split("/");
            var package = GetOrAddPackage(parts[0], out var transitive);
            package.CanonicalName = parts[0];

            var version = package.Versions.GetOrAdd(parts[1], () =>
            {
                var v = new PackageVersion(package, parts[1]);
                transitive = true;
                return v;
            });
            version.CanonicalId = canonicalId;
            var tfmDep = package.Frameworks.GetOrAdd(Tfm, () =>
            {
                transitive = true;
                return new FrameworkDependency(Tfm, version.String);
            });

            Verbose(version.CanonicalId);
            if (_missingDeps.TryGetValue(version.Id, out var expecting))
            {
                foreach (var e in expecting)
                {
                    e.Deps.Add(package);
                }

                _missingDeps.Remove(version.Id);
            }
            else if (transitive)
            {
                _unusedDeps[version.Id] = version;
            }

            if (!version.AllFiles.Any())
            {
                // only do this once per package, we'll be calling this code one per framework that depends on this
                // package
                if (!libraries.GetRequired(version.CanonicalId, out var meta)) return false;
                var allFiles = meta.GetProperty("files");
                version.AllFiles.AddRange(allFiles.EnumerateArray().Select(f => f.GetString()));
            }

            AddPackageDeps(desc, tfmDep);
            return true;
        }

        private Package GetOrAddPackage(string name, out bool addedForTfm)
        {
            var added = false;
            var package = _parent.AllPackages.GetOrAdd(name, () => new Package(name));
            _tfmPackages.GetOrAdd(name, () =>
            {
                added = true;
                return package;
            });
            addedForTfm = added;
            return package;
        }

        private void AddPackageDeps(JsonProperty desc, FrameworkDependency tfmDep)
        {
            if (!desc.Value.TryGetProperty("dependencies", out var deps)) return;
            foreach (var dep in deps.EnumerateObject())
            {
                var canonicalName = dep.Name;
                var versionString = dep.Value.GetString();
                if (versionString[0] == '[')
                {
                    var parts = versionString.Split(",");
                    if (parts.Length > 1)
                    {
                        // "[1.2.3, )"
                        versionString = parts[0][1..];
                    }
                    else
                    {
                        // "[1.2.3]"
                        versionString = versionString[1..^1];
                    }
                }

                var id = canonicalName + "/" + versionString;
                Verbose($"    -> {id}");
                _unusedDeps.Remove(id);
                var package = GetOrAddPackage(canonicalName, out var addedForTfm);

                if (addedForTfm || !package.Versions.TryGetValue(versionString, out _))
                {
                    Verbose("Missing");
                    _missingDeps.GetOrAdd(id, () => new List<FrameworkDependency>()).Add(tfmDep);
                    continue;
                }

                tfmDep.Deps.Add(package);
            }
        }

        private bool RecordTfmInfo(JsonElement assets)
        {
            var anchor = assets;
            foreach (var part in new[] {"project", "frameworks", Tfm})
            {
                if (!anchor.GetRequired(part, out anchor)) return false;
            }

            var info = new TfmInfo(Tfm);

            PackageVersion AddImplicitDep(JsonElement dep, string name)
            {
                // "version": "[2.0.3, )"
                var versionSpec = dep.GetProperty("version").GetString();
                var versionString = versionSpec.Split(",")[0][1..];

                var (pkg, version) = AddPackage(name, versionString);
                info.ImplicitDeps.Add(pkg);

                return version;
            }

            if (anchor.TryGetProperty("dependencies", out var deps))
            {
                foreach (var dep in deps.EnumerateObject())
                {
                    if (!dep.Value.TryGetProperty("autoReferenced", out var auto) || !auto.GetBoolean()) continue;
                    AddImplicitDep(dep.Value, dep.Name);
                }
            }

            if (anchor.TryGetProperty("downloadDependencies", out deps))
            {
                foreach (var dep in deps.EnumerateArray())
                {
                    var package = AddImplicitDep(dep, dep.GetProperty("name").GetString());
                    WalkPackage(package);
                }
            }

            if (anchor.TryGetProperty("frameworkReferences", out var refs))
            {
                // "frameworkReferences": {"Microsoft.NETCore.App":{}}
                var tfn = refs.EnumerateObject().ToList().Single().Name;
                info.Tfn = tfn;
            }

            _parent.Tfms[Tfm] = info;
            return true;
        }

        private (Package pkg, PackageVersion version) AddPackage(string name, string version)
        {
            var package = GetOrAddPackage(name, out _);
            var pkgVersion = new PackageVersion(package, version);
            package.Versions[version] = pkgVersion;
            package.Frameworks.GetOrAdd(Tfm, () => new FrameworkDependency(Tfm, version));
            return (package, pkgVersion);
        }

        private void WalkPackage(PackageVersion version)
        {
            var root = Path.Combine(_parent.PackagesFolder, version.Id.ToLower());

            void Walk(string path)
            {
                foreach (var directory in Directory.EnumerateDirectories(path))
                {
                    Walk(directory);
                }

                foreach (var file in Directory.EnumerateFiles(path))
                {
                    var subPath = file[(root.Length + 1)..];
                    version.AllFiles.Add(subPath);
                }
            }

            Walk(root);
        }
    }
}