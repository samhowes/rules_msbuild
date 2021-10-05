using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static NuGetParser.Package;

namespace NuGetParser
{
    public class Parser
    {
        public readonly string PackagesFolder;
        public readonly Dictionary<string, Package> AllPackages = PackageDict();
        public readonly Dictionary<string, TfmInfo> Tfms = new Dictionary<string, TfmInfo>();
        private readonly Files _files;
        private readonly Action<string> _writeLine;
        private readonly AssetsReader _assetsReader;

        public Parser(string packagesFolder, Files files, Action<string> writeLine, AssetsReader assetsReader)
        {
            PackagesFolder = packagesFolder;
            _files = files;
            _writeLine = writeLine;
            _assetsReader = assetsReader;
        }

        public bool Parse(List<FrameworkInfo> frameworks, Dictionary<string, string> args)
        {
            // explicitly load all requested packages first so we populate the requested name field properly
            LoadPackages(frameworks);

            // now load the full closure of packages
            // foreach (var tfmParser in tfms)
            // {
            //     if (!Try(tfmParser.Tfm, () => tfmParser.ProcessPackages())) return false;
            // }

            if (!GenerateBuildFiles(args)) return false;

            return true;

            bool Try(string tfm, Func<bool> func)
            {
                try
                {
                    return func();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to process packages for tfm {tfm}, please file an issue.");
                    Console.WriteLine(ex.ToString());
                    return false;
                }
            }
        }

        public Dictionary<string, Package> LoadPackages(List<FrameworkInfo> frameworks)
        {
            foreach (var framework in frameworks)
            {
                var info = new TfmInfo(framework.Tfm);
                Tfms[info.Tfm] = info;
                foreach (var restoreGroup in framework.RestoreGroups)
                {
                    LoadRestoreGroup(restoreGroup, framework, info);
                }
            }

            return AllPackages;
        }

        private void LoadRestoreGroup(FrameworkRestoreGroup restoreGroup, FrameworkInfo framework, TfmInfo info)
        {
            foreach (var (requestedName, _) in restoreGroup.Packages)
            {
                AllPackages.GetOrAdd(requestedName, () => new Package(requestedName));
            }
            
            var packages = new Dictionary<string, PackageVersion>(StringComparer.OrdinalIgnoreCase);
            _assetsReader.Init(restoreGroup.ObjDirectory, framework.Tfm);
            foreach (var packageVersion in _assetsReader.GetPackages())
            {
                var version = packageVersion;
                var package = AllPackages.GetOrAdd(version.Id.Name, () =>
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
                var package = AllPackages.GetOrAdd(implicitDep.Name, () => new Package(implicitDep.Name));
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
            var root = Path.Combine(PackagesFolder, version.Id.String.ToLower());

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

        private bool GenerateBuildFiles(Dictionary<string, string> args)
        {
            var allFiles = new List<string>();
            var packagesName = Path.GetFileName(PackagesFolder);
            foreach (var pkg in AllPackages.Values.OrderBy(p => p.RequestedName))
            {
                var buildPath = Path.Join(Path.GetDirectoryName(PackagesFolder), pkg.RequestedName, "BUILD.bazel");
                Directory.CreateDirectory(Path.GetDirectoryName(buildPath));
                using var b = new BuildWriter(File.Create(buildPath));
                b.Load("@rules_msbuild//dotnet:defs.bzl", "nuget_package_download", "nuget_package_framework_version",
                    "nuget_package_version");
                b.Visibility();
                b.StartRule("nuget_package_download", pkg.RequestedName);


                var frameworkVersions = pkg.Versions.Values.SelectMany(
                        v => v.Deps.Select(d => (v, Tfm: d.Key, label: $"{v.Id.Version}-{d.Key}")))
                    .ToList();

                b.SetAttr("framework_versions", frameworkVersions.Select((t) => ":" + t.label));
                b.EndRule();

                foreach (var frameworkVersion in frameworkVersions)
                {
                    b.StartRule("nuget_package_framework_version", frameworkVersion.label);
                    b.SetAttr("version", ":" + frameworkVersion.v.Id.Version);
                    b.SetAttr("deps",
                        frameworkVersion.v.Deps[frameworkVersion.Tfm]
                            .Select(d => $"@nuget//{d.Name}:{d.Version}-{frameworkVersion.Tfm}").OrderBy(d => d));
                    b.EndRule();
                }

                foreach (var version in pkg.Versions.Values.OrderBy(v => v.Id.String))
                {
                    b.StartRule("nuget_package_version", version.Id.Version);
                    var paths = version.AllFiles.Select(f =>
                            string.Join("/", packagesName, version.Id.String.ToLower(), f))
                        .ToList();
                    allFiles.AddRange(paths);
                    var labels = paths.Select(p => "//:" + p);
                    b.SetAttr("all_files", labels);
                    b.EndRule();
                }
            }

            WriteMainBuild(allFiles, args);

            return true;
        }

        private void WriteMainBuild(List<string> allFiles, Dictionary<string, string> args)
        {
            using var b =
                new BuildWriter(File.Create(Path.Join(Path.GetDirectoryName(PackagesFolder), "BUILD.bazel")));
            b.Load("@rules_msbuild//dotnet:defs.bzl", "tfm_mapping", "framework_info");
            b.Visibility();

            b.StartRule("filegroup", "bazel_packages");
            b.SetAttrRaw("srcs", "glob([\"bazel_packages/**/*\"])");
            b.EndRule();

            b.StartRule("tfm_mapping", "tfm_mapping");
            b.SetAttr("frameworks", Tfms.OrderBy(t => t.Key).Select(t => ":" + t.Key));
            b.EndRule();

            foreach (var (tfm, info) in Tfms)
            {
                b.StartRule("framework_info", tfm);
                b.SetAttr("implicit_deps", info.ImplicitDeps.Select(d => d.Label).Distinct().OrderBy(d => d));
                b.EndRule();
            }

            b.StartRule("alias", "test_logger");
            b.SetAttr("actual", "//" + args["test_logger"].Split("/")[0]);
            b.EndRule();

            b.Raw($"exports_files([\"{args["nuget_build_config"]}\"])");

            b.InlineCall("exports_files", b.BzlValue(allFiles, prefix: ""));
        }
    }
}