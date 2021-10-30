using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NuGetParser
{
    public class BuildGenerator
    {
        private readonly NuGetContext _context;
        private readonly string _testLogger;
        private readonly string _nugetBuildConfig;

        public BuildGenerator(NuGetContext context)
        {
            _context = context;
            _testLogger = context.Args["test_logger"];
            _nugetBuildConfig = context.Args["nuget_build_config"];
        }

        public void GenerateBuildFiles()
        {
            var allFiles = GeneratePackageFiles();

            WriteMainBuild(allFiles);
        }

        private List<string> GeneratePackageFiles()
        {
            var allFiles = new List<string>();
            var packagesName = Path.GetFileName(_context.PackagesFolder);
            foreach (var pkg in _context.AllPackages.Values.OrderBy(p => p.RequestedName))
            {
                var buildPath = Path.Join(Path.GetDirectoryName(_context.PackagesFolder), pkg.RequestedName, "BUILD.bazel");
                Directory.CreateDirectory(Path.GetDirectoryName(buildPath));
                using var b = new BuildWriter(File.Create(buildPath));
                b.Load("@rules_msbuild//dotnet:defs.nuget.bzl", "nuget_package_download",
                    "nuget_package_framework_version", "nuget_package_version");
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

            return allFiles;
        }

        private void WriteMainBuild(List<string> allFiles)
        {
            using var b =
                new BuildWriter(File.Create(Path.Join(Path.GetDirectoryName(_context.PackagesFolder), "BUILD.bazel")));
            b.Load("@rules_msbuild//dotnet:defs.nuget.bzl", "tfm_mapping", "framework_info");
            b.Visibility();

            b.StartRule("filegroup", "bazel_packages");
            b.SetAttrRaw("srcs", "glob([\"bazel_packages/**/*\"])");
            b.EndRule();

            b.StartRule("tfm_mapping", "tfm_mapping");
            b.SetAttr("frameworks", _context.Tfms.OrderBy(t => t.Key).Select(t => ":" + t.Key));
            b.EndRule();

            foreach (var (tfm, info) in _context.Tfms)
            {
                b.StartRule("framework_info", tfm);
                b.SetAttr("implicit_deps", info.ImplicitDeps.Select(d => d.Label).Distinct().OrderBy(d => d));
                b.EndRule();
            }

            b.StartRule("alias", "test_logger");
            b.SetAttr("actual", "//" + _testLogger.Split("/")[0]);
            b.EndRule();

            b.Raw($"exports_files([\"{_nugetBuildConfig}\"])");

            b.InlineCall("exports_files", b.BzlValue(allFiles, prefix: ""));
        }
    }
}