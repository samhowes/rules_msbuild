using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace RulesMSBuild.Tools.Builder
{
    public class BazelPropsWriter
    {
        public void WriteRunfilesProps(string path, string[] runfilesEntries)
        {
            var items = new List<XElement>(runfilesEntries.Length);
            var cwd = Directory.GetCurrentDirectory();
            foreach (var entry in runfilesEntries)
            {
                var parts = entry.Split(' ');
                var manifestPath = parts[0];
                var filePath = Path.Combine(cwd, parts[1]);

                items.Add(new XElement("None", new XAttribute("Include", filePath),
                    new XElement("Pack", "true"),
                    new XElement("PackagePath", $"content/runfiles/{manifestPath}")));
            }

            var xml =
                new XElement("Project",
                    new XElement("ItemGroup", items));

            WriteXml(xml, path);
        }

        public void WriteProperties( string path, Dictionary<string, string> properties)
        {
            var props = properties
                .Select((pair) => new XElement(pair.Key, pair.Value));

            var xml =
                new XElement("Project",
                    new XElement("PropertyGroup", props));
            WriteXml(xml, path);
        }

        private void WriteXml(XElement xml, string path)
        {
            using var writer = XmlWriter.Create(path, 
                new XmlWriterSettings()
                {
                    OmitXmlDeclaration = true,
                    Indent = true
                });
            xml.Save(writer);
            writer.Flush();
        }
    }
}