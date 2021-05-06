using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace NuGetParser
{
    public class Parser
    {
        private static Dictionary<string, Package> PackageDict() => PackageDict<Package>();

        private static Dictionary<string, T> PackageDict<T>() =>
            new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);

        private readonly string _intermediateBase;
        private readonly string _packagesFolder;
        private readonly Dictionary<string, string> _args;
        private readonly Dictionary<string, Package> _allPackages = PackageDict();
        private readonly Dictionary<string, TfmInfo> _tfms = new Dictionary<string, TfmInfo>();

        public Parser(string intermediateBase, string packagesFolder, Dictionary<string,string> args)
        {
            _intermediateBase = intermediateBase;
            _packagesFolder = packagesFolder;
            _args = args;
        }

        public bool Parse(List<string> projects)
        {
            foreach (var projectPath in projects)
            {
                Console.WriteLine(projectPath);
                var tfm = Path.GetFileNameWithoutExtension(projectPath);
                var tfmDict = PackageDict();
                var project = XDocument.Load(projectPath);
                foreach (var reference in project.Descendants("PackageReference"))
                {
                    var package = new Package(
                        reference.Attribute("Include")!.Value,
                        reference.Attribute("Version")!.Value);
                    tfmDict[package.Id] = package;
                    _allPackages[package.Id] = package;
                }

                var assets =
                    JsonSerializer.Deserialize<JsonElement>(
                        File.ReadAllText(Path.Combine(_intermediateBase, tfm,
                            "project.assets.json")));

                if (!assets.GetRequired("version", out var version)) return false;

                if (version.GetInt32() != 3)
                {
                    Console.WriteLine($"Unsupported project.assets.json version {version.GetInt32()}");
                    return false;
                }
                if (!RecordTfmInfo(assets, tfm)) return false;

                if (!ProcessAssets(tfm, assets)) return false;
            }

            if (!GenerateBuildFiles()) return false;

            return true;
        }

        private bool GenerateBuildFiles()
        {
            var allFiles = new List<string>();
            var packagesName = Path.GetFileName(_packagesFolder);
            foreach (var package in _allPackages.Values.OrderBy(p => p.Id))
            {
                var buildPath = Path.Join(Path.GetDirectoryName(_packagesFolder), package.RequestedName, "BUILD.bazel");
                Directory.CreateDirectory(Path.GetDirectoryName(buildPath));
                using var buildFile = new BuildWriter(File.Create(buildPath));
                buildFile.Load("@my_rules_dotnet//dotnet:defs.bzl" ,"nuget_filegroup", "nuget_import");
                buildFile.Visibility();
                buildFile.StartRule("nuget_import", package.RequestedName);
                
                var paths = package.AllFiles.Select(f => string.Join("/", packagesName, package.Id.ToLower(), f)).ToList();
                allFiles.AddRange(paths);
                var labels = paths.Select(p => "//:" + p);
                buildFile.SetAttr("all_files", labels);
                buildFile.SetAttr("frameworks", package.Deps.Keys.Select(d => ":" + d));
                buildFile.SetAttr("version", package.Version);
                buildFile.EndRule();
                
                foreach (var (tfm, dep) in package.Deps.OrderBy(k => k.Key))
                {
                    buildFile.StartRule("nuget_filegroup", tfm);
                    buildFile.SetAttr("deps", dep.OrderBy(d => d.Id).Select(d => d.Label));
                    buildFile.EndRule();
                }
            }

            using var b =
                new BuildWriter(File.Create(Path.Join(Path.GetDirectoryName(_packagesFolder), "BUILD.bazel")));
            b.Load("@my_rules_dotnet//dotnet/private/rules:nuget.bzl", "tfm_mapping", "framework_info");
            b.Visibility();
            b.StartRule("tfm_mapping", "tfm_mapping");
            b.SetAttr("frameworks", _tfms.OrderBy(t => t.Key).Select(t => ":" + t.Key));
            b.EndRule();

            foreach (var (tfm, info) in _tfms)
            {
                b.StartRule("framework_info", tfm);
                b.SetAttr("implicit_deps", info.ImplicitDeps.Select(d => d.Label));
                b.EndRule();
            }
            
            b.StartRule("alias", "test_logger");
            b.SetAttr("actual", "//" + _args["test_logger"]);
            b.EndRule();

            b.Raw($"exports_files([\"{_args["nuget_build_config"]}\"])");

            b.InlineCall("exports_files", b.BzlValue(allFiles, prefix:""));
            
            return true;
        }

        private bool RecordTfmInfo(JsonElement assets, string tfm)
        {
            var anchor = assets;
            foreach (var part in new[] {"project", "frameworks", tfm})
            {
                if (!anchor.GetRequired(part, out anchor)) return false;
            }

            var info = new TfmInfo(tfm);

            Package AddImplictDep(JsonElement dep, string name)
            {
                // "version": "[2.0.3, )"
                var versionSpec = dep.GetProperty("version").GetString();
                var version = versionSpec.Split(",")[0][1..];
                var package = new Package(name, version);
                info.ImplicitDeps.Add(package);
                package.Deps.GetOrAdd(tfm, () => new List<Package>());
                _allPackages[package.Id] = package;
                return package;
            }

            if (anchor.TryGetProperty("dependencies", out var deps))
            {
                foreach (var dep in deps.EnumerateObject())
                {
                    if (!dep.Value.TryGetProperty("autoReferenced", out var auto) || !auto.GetBoolean()) continue;
                    AddImplictDep(dep.Value, dep.Name);
                }
            }

            if (anchor.TryGetProperty("downloadDependencies", out deps))
            {
                foreach (var dep in deps.EnumerateArray())
                {
                    var package = AddImplictDep(dep, dep.GetProperty("name").GetString());
                    WalkPackage(package);
                }
            }

            if (anchor.TryGetProperty("frameworkReferences", out var refs))
            {
                // "frameworkReferences": {"Microsoft.NETCore.App":{}}
                var tfn = refs.EnumerateObject().ToList().Single().Name;
                info.Tfn = tfn;
            }

            _tfms[tfm] = info;
            return true;
        }

        private void WalkPackage(Package package)
        {
            var root = Path.Combine(_packagesFolder, package.Id.ToLower());

            void Walk(string path)
            {
                foreach (var directory in Directory.EnumerateDirectories(path))
                {
                    Walk(directory);
                }

                foreach (var file in Directory.EnumerateFiles(path))
                {
                    var subPath = file[(root.Length + 1)..];
                    package.AllFiles.Add(subPath);
                }
            }

            Walk(root);
        }

        private bool ProcessAssets(string tfm, JsonElement assets)
        {
            var discoveredDeps = PackageDict();
            var unusedDeps = PackageDict();
            var missingDeps = PackageDict<List<Package>>();
            var tfmPackages = assets.GetProperty("targets").EnumerateObject().Single().Value;
            if (!assets.GetRequired("libraries", out var libraries)) return false;


            foreach (var desc in tfmPackages.EnumerateObject())
            {
                var canonicalId = desc.Name;
                var type = desc.Value.GetProperty("type");
                if (type.GetString() != "package")
                {
                    Console.WriteLine($"Unexpected dependency type: {type}");
                    return false;
                }

                bool transitive = false;
                var package = _allPackages.GetOrAdd(canonicalId, () =>
                {
                    var parts = canonicalId.Split("/");
                    var p = new Package(parts[0], parts[1]);
                    _allPackages[p.Id] = p;
                    transitive = true;
                    return p;
                });
                package.Deps.GetOrAdd(tfm, () => new List<Package>());
                package.CanonicalId = canonicalId;

                discoveredDeps[package.Id] = package;
                if (missingDeps.TryGetValue(package.Id, out var expecting))
                {
                    foreach (var e in expecting)
                    {
                        e.Deps.GetOrAdd(tfm, () => new List<Package>()).Add(package);
                    }

                    missingDeps.Remove(package.Id);
                }
                else if (transitive)
                {
                    unusedDeps[package.Id] = package;
                }

                if (desc.Value.TryGetProperty("dependencies", out var deps))
                {
                    foreach (var dep in deps.EnumerateObject())
                    {
                        var canonicalName = dep.Name;
                        var version = dep.Value.GetString();
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
                        var id = canonicalName + "/" + version;
                        unusedDeps.Remove(id);

                        if (!discoveredDeps.TryGetValue(id, out var pkg))
                        {
                            missingDeps.GetOrAdd(id, () => new List<Package>()).Add(package);
                            continue;
                        }

                        package.Deps.GetOrAdd(tfm, () => new List<Package>()).Add(pkg);
                    }
                }

                if (!package.AllFiles.Any())
                {
                    // only do this once per package, we'll be calling this code one per framework that depends on this
                    // package
                    if (!libraries.GetRequired(package.CanonicalId, out var meta)) return false;
                    var allFiles = meta.GetProperty("files");
                    package.AllFiles.AddRange(allFiles.EnumerateArray().Select(f => f.GetString()));
                }
            }

            if (unusedDeps.Any())
            {
                Console.WriteLine($"Found unused deps for target framework {tfm}: {string.Join(";", unusedDeps.Keys)}");
                return false;
            }

            if (missingDeps.Any())
            {
                var byName = PackageDict();
                foreach (var (_, p) in discoveredDeps)
                {
                    if (byName.ContainsKey(p.RequestedName))
                    {
                        Console.WriteLine($"Detected multiple versions of the same package: {p.RequestedName}. Please file an issue to support this use case.");
                        return false;
                    }

                    byName[p.RequestedName] = p;
                }
                foreach (var (id, expecting) in missingDeps)
                {
                    if (byName.TryGetValue(id.Split("/")[0], out var versionUpgrade))
                    {
                        foreach (var e in expecting)
                        {
                            e.Deps.GetOrAdd(tfm, () => new List<Package>()).Add(versionUpgrade);
                        }

                        missingDeps.Remove(id);
                    }
                }

                if (missingDeps.Any())
                {
                    Console.WriteLine($"Found packages expecting dependencies, but didn't find the dependencies:");
                    foreach (var (id, expecting) in missingDeps)
                    {
                        Console.WriteLine($"{id} <= {string.Join(", ", expecting.Select(e => e.Id))}");
                    }

                    return false;
                }
            }

            return true;
        }
    }
}