using System;
using System.Collections.Generic;
using System.IO;
using static NuGetParser.Package;

namespace NuGetParser
{
    public class NuGetContext
    {
        public Dictionary<string, string> Args { get; }
        public List<FrameworkInfo> Frameworks { get; set; }

        public readonly string PackagesFolder;
        public string DotnetRoot;
        public readonly Dictionary<string, Package> AllPackages = PackageDict();
        public readonly Dictionary<string, TfmInfo> Tfms = new Dictionary<string, TfmInfo>();

        public NuGetContext(Dictionary<string, string> args)
        {
            Args = args;
            PackagesFolder = args["packages_folder"];
            DotnetRoot = Path.GetDirectoryName(Args["dotnet_path"])!;
        }
    }

    public class Parser
    {
        private readonly NuGetContext _context;
        private readonly Files _files;
        private readonly AssetsReader _assetsReader;

        public Parser(NuGetContext context, Files files, AssetsReader assetsReader)
        {
            _context = context;
            _files = files;
            _assetsReader = assetsReader;
        }

        public void Parse()
        {
            foreach (var framework in _context.Frameworks)
            {
                var info = new TfmInfo(framework.Tfm);
                _context.Tfms[info.Tfm] = info;
                foreach (var restoreGroup in framework.RestoreGroups)
                {
                    LoadRestoreGroup(restoreGroup, framework, info);
                }
            }
        }

        private void LoadRestoreGroup(FrameworkRestoreGroup restoreGroup, FrameworkInfo framework, TfmInfo info)
        {
            foreach (var (requestedName, _) in restoreGroup.Packages)
            {
                _context.AllPackages.GetOrAdd(requestedName, () => new Package(requestedName));
            }

            var packages = new Dictionary<string, PackageVersion>(StringComparer.OrdinalIgnoreCase);
            _assetsReader.Init(restoreGroup.ObjDirectory, framework.Tfm);
            foreach (var packageVersion in _assetsReader.GetPackages())
            {
                var version = packageVersion;
                var package = _context.AllPackages.GetOrAdd(version.Id.Name, () =>
                {
                    var p = new Package(packageVersion.Id.Name);
                    return p;
                });

                if (package.Versions.TryGetValue(packageVersion.Id.Version, out var existingVersion))
                {
                    existingVersion.Deps[framework.Tfm] = packageVersion.Deps[framework.Tfm];
                    version = existingVersion;
                }
                else
                {
                    package.Versions[packageVersion.Id.Version] = packageVersion;
                }

                // record locally for auto version upgrades
                packages[version.Id.Name] = version;
            }

            foreach (var implicitDep in _assetsReader.GetImplicitDependencies())
            {
                var package = _context.AllPackages.GetOrAdd(implicitDep.Name, () => new Package(implicitDep.Name));
                var version = package.Versions.GetOrAdd(implicitDep.Version, () =>
                {
                    var v = new PackageVersion(implicitDep);
                    package.Versions[implicitDep.Version] = v;
                    WalkPackage(v);
                    return v;
                });
                version.Deps.GetOrAdd(framework.Tfm, () => new List<PackageId>());
                info.ImplicitDeps.Add(package);
            }

            // now process deps for version upgrades
            foreach (var packageVersion in packages.Values)
            {
                if (!packageVersion.Deps.TryGetValue(framework.Tfm, out var deps)) continue;

                for (var i = 0; i < deps.Count; i++)
                {
                    var dep = deps[i];
                    var actual = packages[dep.Name];

                    // nuget chose a different version for this package (likely an upgrade)
                    if (actual.Id.Version != dep.Version)
                        deps[i] = actual.Id;
                }
            }
        }

        private void WalkPackage(PackageVersion version)
        {
            var root = Path.Combine(_context.PackagesFolder, version.Id.String.ToLower());

            void Walk(string path)
            {
                foreach (var directory in _files.EnumerateDirectories(path))
                {
                    Walk(directory);
                }

                foreach (var file in _files.EnumerateFiles(path))
                {
                    var subPath = file[(root.Length + 1)..];
                    if (Path.DirectorySeparatorChar == '\\')
                        subPath = subPath.Replace('\\', '/');
                    version.AllFiles.Add(subPath);
                }
            }

            Walk(root);
        }
    }
}