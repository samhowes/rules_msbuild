using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace RulesMSBuild.Tools.Builder
{
    public class BazelPropsWriter
    {
        public void WriteProperties( string path, Dictionary<string, string> properties)
        {
            var props = properties
                .Select((pair) => new XElement(pair.Key, pair.Value));

            var xml =
                new XElement("Project",
                    new XElement("PropertyGroup", props));
            WriteXml(xml, path);
        }

        public void WriteTargets(string path)
        {
            var xml =
                new XElement("Project",
                    new XElement("ItemGroup",
                        new XElement("PackageReference",
                            new XAttribute("Update", "Runfiles"),
                            new XAttribute("Version", Environment.GetEnvironmentVariable("RULES_MSBUILD_VERSION")!))));
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