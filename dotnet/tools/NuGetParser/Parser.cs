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

        public Parser(string intermediateBase, string packagesFolder, Dictionary<string, string> args)
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
                try
                {
                    if (!ProcessTfmPackages(projectPath, tfm)) return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to process packages for tfm {tfm}, please file an issue.");
                    Console.WriteLine(ex.ToString());
                    return false;
                }
            }

            if (!GenerateBuildFiles()) return false;

            return true;
        }

        private bool ProcessTfmPackages(string projectPath, string tfm)
        {
            var project = XDocument.Load(projectPath);
            foreach (var reference in project.Descendants("PackageReference"))
            {
                var packageName = reference.Attribute("Include")!.Value;
                var versionString = reference.Attribute("Version")!.Value;
                AddPackage(tfm, packageName, versionString);
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
            return true;
        }

        private bool GenerateBuildFiles()
        {
            var allFiles = new List<string>();
            var packagesName = Path.GetFileName(_packagesFolder);
            foreach (var pkg in _allPackages.Values.OrderBy(p => p.RequestedName))
            {
                var buildPath = Path.Join(Path.GetDirectoryName(_packagesFolder), pkg.RequestedName, "BUILD.bazel");
                Directory.CreateDirectory(Path.GetDirectoryName(buildPath));
                using var b = new BuildWriter(File.Create(buildPath));
                b.Load("@my_rules_dotnet//dotnet:defs.bzl", "nuget_filegroup", "nuget_import", "nuget_package_version");
                b.Visibility();
                b.StartRule("nuget_import", pkg.RequestedName);

                var frameworks = pkg.Frameworks.Values.OrderBy(f => f.Tfm).ToList();
                b.SetAttr("frameworks", frameworks.Select(f => ":" + f.Tfm));
                b.EndRule();

                foreach (var framework in frameworks)
                {
                    b.StartRule("nuget_filegroup", framework.Tfm);
                    b.SetAttr("version", ":" + framework.Version);
                    b.SetAttr("deps", framework.Deps.Select(d => d.Label).OrderBy(d => d));
                    b.EndRule();
                }

                foreach (var version in pkg.Versions.Values.OrderBy(v => v.String))
                {
                    b.StartRule("nuget_package_version", version.String);
                    var paths = version.AllFiles.Select(f =>
                            string.Join("/", packagesName, pkg.RequestedName.ToLower(), version.String.ToLower(), f))
                        .ToList();
                    allFiles.AddRange(paths);
                    var labels = paths.Select(p => "//:" + p);
                    b.SetAttr("all_files", labels);
                    b.EndRule();
                }
            }

            WriteMainBuild(allFiles);

            return true;
        }

        private void WriteMainBuild(List<string> allFiles)
        {
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

            b.InlineCall("exports_files", b.BzlValue(allFiles, prefix: ""));
        }

        private bool RecordTfmInfo(JsonElement assets, string tfm)
        {
            var anchor = assets;
            foreach (var part in new[] {"project", "frameworks", tfm})
            {
                if (!anchor.GetRequired(part, out anchor)) return false;
            }

            var info = new TfmInfo(tfm);

            PackageVersion AddImplicitDep(JsonElement dep, string name)
            {
                // "version": "[2.0.3, )"
                var versionSpec = dep.GetProperty("version").GetString();
                var versionString = versionSpec.Split(",")[0][1..];
                
                var (pkg, version) = AddPackage(tfm, name, versionString);
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

            _tfms[tfm] = info;
            return true;
        }

        private (Package pkg, PackageVersion version) AddPackage(string tfm, string name, string version)
        {
            var package = _allPackages.GetOrAdd(name, () => new Package(name));
            var pkgVersion = new PackageVersion(package, version);
            package.Versions[version] = pkgVersion;
            package.Frameworks.GetOrAdd(tfm, () => new FrameworkDependency(tfm, version));
            return (package, pkgVersion);
        }

        private void WalkPackage(PackageVersion version)
        {
            var root = Path.Combine(_packagesFolder, version.Id.ToLower());

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

        private bool ProcessAssets(string tfm, JsonElement assets)
        {
            var unusedDeps = PackageDict<PackageVersion>();
            var missingDeps = PackageDict<List<FrameworkDependency>>();
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
                
                var parts = canonicalId.Split("/");
                bool transitive = false;
                var package = _allPackages.GetOrAdd(parts[0], () =>
                {
                    transitive = true;
                    return new Package(parts[0]);
                });
                package.CanonicalName = parts[0];
                
                var version = package.Versions.GetOrAdd(parts[1], () =>
                {
                    var v = new PackageVersion(package, parts[1]);
                    transitive = true;
                    return v;
                });
                version.CanonicalId = canonicalId;
                var tfmDep = package.Frameworks.GetOrAdd(tfm, () =>
                {
                    transitive = true;
                    return new FrameworkDependency(tfm, version.String);
                });
                
                if (missingDeps.TryGetValue(version.Id, out var expecting))
                {
                    foreach (var e in expecting)
                    {
                        e.Deps.Add(package);
                    }

                    missingDeps.Remove(version.Id);
                }
                else if (transitive)
                {
                    unusedDeps[version.Id] = version;
                }

                if (!version.AllFiles.Any())
                {
                    // only do this once per package, we'll be calling this code one per framework that depends on this
                    // package
                    if (!libraries.GetRequired(version.CanonicalId, out var meta)) return false;
                    var allFiles = meta.GetProperty("files");
                    version.AllFiles.AddRange(allFiles.EnumerateArray().Select(f => f.GetString()));
                }
                
                AddPackageDeps(desc, unusedDeps, missingDeps, tfmDep);
            }

            if (unusedDeps.Any())
            {
                Console.WriteLine($"Found unused deps for target framework {tfm}: {string.Join(";", unusedDeps.Keys)}");
                return false;
            }

            if (missingDeps.Any())
            {
                foreach (var (id, expecting) in missingDeps)
                {
                    if (_allPackages.TryGetValue(id.Split("/")[0], out var versionUpgrade))
                    {
                        foreach (var e in expecting)
                        {
                            e.Deps.Add(versionUpgrade);
                        }

                        missingDeps.Remove(id);
                    }
                }

                if (missingDeps.Any())
                {
                    Console.WriteLine($"Found packages expecting dependencies, but didn't find the dependencies:");
                    foreach (var (id, expecting) in missingDeps)
                    {
                        Console.WriteLine($"{id} <= {string.Join(", ", expecting.Select(e => e.Tfm))}");
                    }

                    return false;
                }
            }

            return true;
        }

        private void AddPackageDeps(JsonProperty desc, 
            Dictionary<string, PackageVersion> unusedDeps, 
            Dictionary<string, List<FrameworkDependency>> missingDeps,
            FrameworkDependency tfmDep)
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
                unusedDeps.Remove(id);
                var package = _allPackages.GetOrAdd(canonicalName, () => new Package(canonicalName));

                if (!package.Versions.TryGetValue(versionString, out var version))
                {
                    missingDeps.GetOrAdd(id, () => new List<FrameworkDependency>()).Add(tfmDep);
                    continue;
                }

                tfmDep.Deps.Add(package);
            }
        }
    }
}