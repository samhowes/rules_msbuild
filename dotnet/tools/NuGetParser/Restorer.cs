#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace NuGetParser
{
    public class ProjectBuilder
    {
        private readonly string _sdk;
        private readonly XElement _project;
        private readonly string? _sdkVersion;

        public ProjectBuilder(string sdk)
        {
            _sdk = sdk;
            var parts = sdk.Split("/");
            if (parts.Length > 1)
            {
                _sdk = parts[0];
                _sdkVersion = parts[1];
            }

            _project = new XElement("Project",
                new XElement("Import",
                    new XAttribute("Project", "Restore.props")),
                Import("Sdk.props")
            );
        }

        private XElement Import(string project)
        {
            var el = new XElement("Import",
                new XAttribute("Project", project),
                new XAttribute("Sdk", _sdk));

            if (_sdkVersion != null)
            {
                el.Add(new XAttribute("Version", _sdkVersion));
            }

            return el;
        }


        public void SetTfm(string tfm)
        {
            _project.Add(new XElement("PropertyGroup",
                new XElement("TargetFramework", tfm)));
        }

        public void AddPackages(IEnumerable<(string name, string version)> packages)
        {
            _project.Add(new XElement("ItemGroup",
                packages.Select(p =>
                {
                    var el = new XElement("PackageReference",
                        new XAttribute("Include", p.name));
                    if (!string.IsNullOrEmpty(p.version))
                        el.Add(new XAttribute("Version", p.version));
                    return el;
                })));   
        }

        public void Save(string path)
        {
            _project.Add(Import("Sdk.targets"));

            using var textWriter = File.CreateText(path);
            using var writer = XmlWriter.Create(textWriter, new XmlWriterSettings() {Indent = true});
            _project.Save(writer);
        }

        public void AddReferences(List<string> projects)
        {
            _project.Add(new XElement("ItemGroup",
                projects.Select(p => new XElement("ProjectReference", 
                    new XAttribute("Include", p)))));
        }
    }
    
    public class Restorer
    {
        private readonly string _specPath;
        private readonly string _dotnetPath;
        private readonly string _testLogger;
        private readonly string _dir;

        public Restorer(string specPath, string dotnetPath, string testLogger)
        {
            _specPath = specPath;
            _dir = Path.GetDirectoryName(specPath)!;
            _dotnetPath = dotnetPath;
            _testLogger = testLogger;
        }

        public List<FrameworkInfo> Restore()
        {
            var (fetchProject, frameworks) = MakeRestoreProjects();
            var dotnet = Process.Start(
                new ProcessStartInfo(_dotnetPath,
                    $"restore {fetchProject}")
                {
                    WorkingDirectory = _dir
                });
            dotnet!.WaitForExit();
            return frameworks.Values.ToList();
        }
        
        private (string fetchPath, Dictionary<string, FrameworkInfo> frameworks) MakeRestoreProjects()
        {
            var specList = File.ReadAllLines(_specPath);
            var frameworks = ParsePackageGroups(specList);
            var dir = Path.GetDirectoryName(_specPath)!;
            var projects = new List<string>();
            foreach (var framework in frameworks.Values)
            {
                for (int i = 0; i < framework.RestoreGroups.Count; i++)
                {
                    var group = framework.RestoreGroups[i];
                    var builder = new ProjectBuilder("Microsoft.NET.Sdk");
                    builder.SetTfm(framework.Tfm);
                    builder.AddPackages(group.Packages.Select(p => (p.Key, p.Value)));
                    
                    var projectFileName = $"{framework.Tfm}.{i + 1}.proj";
                    projects.Add(projectFileName);
                    var path = Path.Combine(dir!, projectFileName);

                    group.ProjectFileName = projectFileName;
                    group.ObjDirectory = Path.Combine(dir, "_obj", Path.GetFileNameWithoutExtension(projectFileName));
                    builder.Save(path);
                }
            }

            var fetchBuilder = new ProjectBuilder("Microsoft.Build.Traversal/3.0.3");
            fetchBuilder.AddReferences(projects);
            var fetchPath = Path.Combine(dir!, "nuget.fetch.proj");
            fetchBuilder.Save(fetchPath);
            return (fetchPath, frameworks);
        }

        private Dictionary<string, FrameworkInfo> ParsePackageGroups(string[] specList)
        {
            var frameworks = new Dictionary<string, FrameworkInfo>(StringComparer.OrdinalIgnoreCase);
            var loggerParts = _testLogger.Split("/");
            var loggerName = loggerParts[0];
            var loggerVersion = loggerParts[1];
            foreach (var spec in specList)
            {
                var parts = spec.Split(":");
                string? packageName = null;
                string packageVersion = null!;
                if (parts.Length > 1)
                {
                    var packageParts = parts[0].Split("/");
                    packageName = packageParts[0];
                    packageVersion = packageParts[1];
                }

                var tfms = parts.Last().Split(",");
                foreach (var tfm in tfms)
                {
                    if (string.IsNullOrEmpty(tfm))
                        throw new CustomException($"Invalid tfm '{tfm}'");
                    
                    if (!frameworks.TryGetValue(tfm, out var tfmInfo))
                    {
                        tfmInfo = new FrameworkInfo(tfm);
                        frameworks[tfm] = tfmInfo;
                        tfmInfo.AddPackage(loggerName, loggerVersion);
                    }

                    if (packageName == null) continue;

                    tfmInfo.AddPackage(packageName, packageVersion);
                }
            }

            return frameworks;
        }
    }
}