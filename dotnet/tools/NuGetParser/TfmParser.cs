using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using static RulesMSBuild.Tools.NuGetParser.BazelLogger;
using static NuGetParser.Package;

namespace NuGetParser
{
    public class Files
    {
        public virtual string GetContents(string path) => File.ReadAllText(path);
        public virtual void WriteContents(string path, string contents) => File.WriteAllText(path, contents);
        public virtual IEnumerable<string> GetFiles(string path) => Directory.EnumerateFiles(path);
        public virtual IEnumerable<string> GetDirectories(string path) => Directory.EnumerateDirectories(path);
        public virtual Stream Create(string path) => File.Create(path);
        public virtual Stream OpenRead(string path) => File.OpenRead(path);

        public virtual bool Exists(string path) => File.Exists(path);

        public virtual IEnumerable<string> EnumerateDirectories(string path)
        {
            return Directory.EnumerateDirectories(path);
        }

        public virtual IEnumerable<string> EnumerateFiles(string path)
        {
            return Directory.EnumerateFiles(path);
        }
    }

    public class TfmParser
    {
        public readonly string Tfm;
        private readonly Parser _parent;
        private readonly Files _files;
        private readonly Action<string> _writeLine;
        private Dictionary<string, PackageVersion> _unusedDeps;
        private Dictionary<string, List<PackageVersion>> _missingDeps;
        public readonly Dictionary<string, Package> TfmPackages = PackageDict();
        private readonly FrameworkInfo _info;
        private Dictionary<string,PackageVersion> _currentPackages;
        private HashSet<string> _currentlyRequestedPackages;

        public TfmParser(FrameworkInfo frameworkInfo, Parser parent, Files files, Action<string> writeLine)
        {
            _info = frameworkInfo;
            Tfm = _info.Tfm;
            _parent = parent;
            _files = files;
            _writeLine = writeLine;
        }

        public bool ProcessPackages()
        {
            foreach (var group in _info.RestoreGroups)
            {
                _missingDeps = PackageDict<List<PackageVersion>>();
                _unusedDeps = PackageDict<PackageVersion>();
                _currentPackages = PackageDict<PackageVersion>();
                _currentlyRequestedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var assets = new AssetsReader(_files);

                var error = assets.Init(group.ObjDirectory, Tfm);
                if (error != null)
                {
                    _writeLine(error);
                    return false;
                }

                var packages = _parent.AllPackages;
                foreach (var packageVersion in assets.GetPackages())
                {
                    if (!packages.TryGetValue(packageVersion.Id.Name, out var package))
                    {
                        // package = new Package()
                    }
                }
                
                if (!RecordTfmInfo(assets)) return false;

                if (!ProcessAssets(assets)) return false;
            }
            
            return true;
        }

        private bool ProcessAssets(AssetsReader assets)
        {
            Debug(Tfm);
            // foreach (var desc in tfmPackagesJson.EnumerateObject())
            // {
            //     if (!Parse(desc, libraries)) return false;
            // }

            if (_missingDeps.Any())
            {
                foreach (var (id, expecting) in _missingDeps)
                {
                    var name = id.Split("/")[0];
                    if (_currentPackages.TryGetValue(name, out var versionUpgrade))
                    {
                        foreach (var e in expecting)
                        {
                            // e.AddDep(Tfm, versionUpgrade);
                        }

                        _missingDeps.Remove(id);
                        // _unusedDeps.Remove(versionUpgrade.Id);
                    }
                }

                if (_missingDeps.Any())
                {
                    _writeLine($"Found packages expecting dependencies, but didn't find the dependencies:");
                    foreach (var (id, expecting) in _missingDeps)
                    {
                        _writeLine($"{id} <= {string.Join(", ", expecting.Select(e => e.Id))}");
                    }

                    return false;
                }
            }
            
            if (_unusedDeps.Any())
            {
                _writeLine(
                    $"Found unused deps for target framework {Tfm}: {string.Join(";", _unusedDeps.Keys)}");
                return false;
            }

            return true;
        }
        

        private Package GetOrAddPackage(string name)
        {
            var package = _parent.AllPackages.GetOrAdd(name, () => new Package(name));
            TfmPackages.GetOrAdd(name, () => package);
            return package;
        }

        private bool RecordTfmInfo(AssetsReader assets)
        {
            var info = new TfmInfo(Tfm);
            _parent.Tfms[Tfm] = info;
            //
            // foreach (var (name, versionString, isImplicit) in assets.GetImplicitDependencies())
            // {
            //     var (pkg, version) = AddPackage(name, versionString);
            //     // version.CanonicalId = $"{name}/{versionString}";
            //     if (isImplicit)
            //         info.ImplicitDeps.Add(pkg);
            // }

            return true;
        }

        public (Package pkg, PackageVersion version) AddPackage(string name, string versionString)
        {
            var package = GetOrAddPackage(name);
            // var pkgVersion = package.Frameworks.GetOrAdd(Tfm, 
            //         () => new FrameworkDependency(Tfm))
            //     .Versions
            //     .GetOrAdd(versionString, () => new PackageVersion(""));
            // package.Versions[versionString] = pkgVersion;
            return (package, null);
        }

        private void WalkPackage(PackageVersion version)
        {
            var root = Path.Combine(_parent.PackagesFolder, version.Id.String.ToLower());
            
            void Walk(string path)
            {
                foreach (var directory in _files.EnumerateDirectories(path))
                {
                    Walk(directory);
                }
            
                foreach (var file in _files.EnumerateFiles(path))
                {
                    var subPath = file[(root.Length + 1)..];
                    version.AllFiles.Add(subPath);
                }
            }
            
            Walk(root);
        }
    }
}