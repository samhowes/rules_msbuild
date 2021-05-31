using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static NuGetParser.Package;

namespace NuGetParser
{
    public class Parser
    {
        public readonly string IntermediateBase;
        public readonly string PackagesFolder;
        private readonly Dictionary<string, string> _args;
        public readonly Dictionary<string, Package> AllPackages = PackageDict();
        public readonly Dictionary<string, TfmInfo> Tfms = new Dictionary<string, TfmInfo>();

        public Parser(string intermediateBase, string packagesFolder, Dictionary<string, string> args)
        {
            IntermediateBase = intermediateBase;
            PackagesFolder = packagesFolder;
            _args = args;
        }

        public bool Parse(List<string> projects)
        {
            var tfms = new List<TfmParser>();
            
            // explicitly load all requested packages first so we populate the requested name field properly
            foreach (var projectPath in projects)
            {
                var tfm = Path.GetFileNameWithoutExtension(projectPath);
                if (!Try(tfm, () =>
                {
                    var parser = new TfmParser(tfm, this);
                    tfms.Add(parser);
                    return parser.LoadRequestedPackages(projectPath);
                })) return false;
            }

            // now load the full closure of packages
            foreach (var tfmParser in tfms)
            {
                if (!Try(tfmParser.Tfm, () => tfmParser.ProcessPackages())) return false;
            }

            if (!GenerateBuildFiles()) return false;

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

        private bool GenerateBuildFiles()
        {
            var allFiles = new List<string>();
            var packagesName = Path.GetFileName(PackagesFolder);
            foreach (var pkg in AllPackages.Values.OrderBy(p => p.RequestedName))
            {
                var buildPath = Path.Join(Path.GetDirectoryName(PackagesFolder), pkg.RequestedName, "BUILD.bazel");
                Directory.CreateDirectory(Path.GetDirectoryName(buildPath));
                using var b = new BuildWriter(File.Create(buildPath));
                b.Load("@my_rules_dotnet//dotnet:defs.bzl", "nuget_package", "nuget_package_framework", "nuget_package_version");
                b.Visibility();
                b.StartRule("nuget_package", pkg.RequestedName);

                var frameworks = pkg.Frameworks.Values.OrderBy(f => f.Tfm).ToList();
                b.SetAttr("frameworks", frameworks.Select(f => ":" + f.Tfm));
                b.EndRule();

                foreach (var framework in frameworks)
                {
                    b.StartRule("nuget_package_framework", framework.Tfm);
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
                new BuildWriter(File.Create(Path.Join(Path.GetDirectoryName(PackagesFolder), "BUILD.bazel")));
            b.Load("@my_rules_dotnet//dotnet/private/rules:nuget.bzl", "tfm_mapping", "framework_info");
            b.Visibility();
            b.StartRule("tfm_mapping", "tfm_mapping");
            b.SetAttr("frameworks", Tfms.OrderBy(t => t.Key).Select(t => ":" + t.Key));
            b.EndRule();

            foreach (var (tfm, info) in Tfms)
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
    }
}