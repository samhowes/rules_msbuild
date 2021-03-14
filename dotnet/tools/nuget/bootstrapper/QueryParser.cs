using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace bootstrapper
{
    public class DotnetTarget
    {
        public Label Label { get; set; }
        public List<string> Tfms { get; set; }
        public List<Label> Deps { get; set; }
    }

    public class QueryParser
    {
        private readonly TextReader _reader;
        private XElement _query;

        public QueryParser(TextReader reader)
        {
            _reader = reader;
        }

        private void InitReader()
        {
            _reader.ReadLine(); // ignore the version header: bazel outputs xml version 1.1, but dotnet core doesn't support 1.1
            var document = XDocument.Load(_reader);
            _query = document.Root;
            var version = _query!.Attribute("version");
            if (_query.Name != "query" || version?.Value != "2")
            {
                throw new Exception($"Unexpected document type, expected <query version=\"2\"> as the root element.");
            }
        }

        public Dictionary<string, HashSet<Label>> GetPackagesByFramework(string repositoryName)
        {
            InitReader();
            var tfms = new Dictionary<string, HashSet<Label>>();
            foreach (var assembly in Targets())
            {
                var repositoryDeps = assembly.Deps
                        .Where(l => l.WorkspaceName == repositoryName)
                        .ToList();

                foreach (var tfm in assembly.Tfms)
                {
                    if (!tfms.TryGetValue(tfm!, out var packages))
                    {
                        packages = new HashSet<Label>();
                        tfms[tfm] = packages;
                    }

                    foreach (var dep in repositoryDeps)
                    {
                        packages.Add(dep);
                    }
                }
            }

            return tfms;
        }

        private IEnumerable<DotnetTarget> Targets(string workspaceName = null)
        {
            foreach (var rule in _query.Descendants("rule"))
            {
                var assembly = new DotnetTarget();
                assembly.Label = new Label(rule.Attribute("name")!.Value);
                if (string.IsNullOrEmpty(assembly.Label.WorkspaceName))
                    assembly.Label.WorkspaceName = workspaceName;

                var lists = rule.Descendants("list");
                var deps = lists
                    .Where(l => l.Attribute("name")?.Value == "deps");
                assembly.Deps = deps
                    .SelectMany(d => d.Descendants("label")
                        .Select(l => l.Attribute("value")?.Value)
                        .Where(l => l != null)
                        .Select(l => new Label(l!))
                     ).ToList();

                assembly.Tfms = rule.Descendants("string")
                    .Where(s => s.Attribute("name")?.Value == "target_framework")
                    .Select(s => s.Attribute("value")?.Value)
                    .Where(s => s != null)
                    .ToList();
                yield return assembly;
            }
        }

        public List<DotnetTarget> GetTargets(string workspaceName)
        {
            InitReader();
            return Targets(workspaceName).ToList();
        }
    }
}