#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace NuGetParser
{
    public class ProjectBuilder
    {
        private readonly string? _sdk;
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
}