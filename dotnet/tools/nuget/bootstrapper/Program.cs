using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace bootstrapper
{
    public class Label
    {
        private static Regex LabelRegex = new Regex("^(@(?<repository>\\w+))?(?<root>//)?(?<package>[^\\:]+)?:?(?<name>.*)");

        public Label(string labelOrPath)
        {
            RawValue = labelOrPath;
            var match = LabelRegex.Match(labelOrPath);
            if (match.Success)
            {
                WorkspaceName = match.Groups["repository"].Value;
                Repository = match.Groups["repository"].Value;
                IsRooted = match.Groups["root"].Success;
                Package = match.Groups["package"].Value ?? "";
                Name = match.Groups["name"].Value;
            }
            else
            {
                IsPath = true;
            }
        }

        public string RawValue { get; set; }

        public bool IsPath { get; set; }

        public string WorkspaceName { get; set; }

        public bool IsRooted { get; set; }

        public string Repository { get; set; }

        public string Name { get; set; }

        public string Package { get; set; }

        public string PathRoot { get; set; }

        public string Filepath { get; set; }
    }

    internal class Program
    {
        private static void Main(string[] args)
        {
            const string repositoryName = "nuget";

            var xmlPath = args[0];
            // if we are running under Bazel run, assume relative to the workspace directory
            var xmlLabel = new Label(xmlPath);
            var bazelEnvironment = new BazelEnvironment();
            bazelEnvironment.ResolveLabel(xmlLabel);

            if (!File.Exists(xmlLabel.Filepath))
            {
                Console.WriteLine($"Invalid path: {xmlPath}");
                Environment.Exit(1);
            }

            var reader = new StreamReader(xmlLabel.Filepath);
            var queryParser = new QueryParser(reader);
            var packagesByTargetFramework = queryParser.GetPackagesByFramework(repositoryName);

            foreach (var tfm in packagesByTargetFramework)
            {
                var outputPath = Path.Join(Path.GetDirectoryName(xmlLabel.Filepath), tfm.Key + "deps.txt");
                Console.WriteLine(outputPath);
                using var txt = new StreamWriter(outputPath);
                foreach (var package in tfm.Value)
                {
                    txt.WriteLine(package.Name);
                }
                txt.Flush();
                txt.Close();
            }
        }
    }

    public class QueryParser
    {
        private readonly TextReader _reader;

        public QueryParser(TextReader reader)
        {
            _reader = reader;
        }

        public Dictionary<string, HashSet<Label>> GetPackagesByFramework(string repositoryName)
        {
            _reader.ReadLine(); // ignore the version header: bazel outputs xml version 1.1, but dotnet core doesn't support 1.1
            var document = XDocument.Load(_reader);
            var query = document.Root;
            var version = query!.Attribute("version");
            if (query.Name != "query" || version?.Value != "2")
            {
                Console.WriteLine($"Unexpected document type, expected <query version=\"2\"> as the root element.");
                Environment.Exit(1);
            }

            var tfms = new Dictionary<string, HashSet<Label>>();
            foreach (var rule in query.Descendants("rule"))
            {
                var lists = rule.Descendants("list");
                var deps = lists
                    .Where(l => l.Attribute("name")?.Value == "deps");
                var depLabels = deps
                    .SelectMany(d => d.Descendants("label")
                        .Select(l => l.Attribute("value")?.Value)
                        .Where(l => l != null)
                        .Select(l => new Label(l))
                        .Where(l => l.WorkspaceName == repositoryName));

                var ruleTfms = rule.Descendants("string")
                    .Where(s => s.Attribute("name")?.Value == "target_framework")
                    .Select(s => s.Attribute("value")?.Value)
                    .Where(s => s != null);

                foreach (var tfm in ruleTfms)
                {
                    if (!tfms.TryGetValue(tfm, out var packages))
                    {
                        packages = new HashSet<Label>();
                        tfms[tfm] = packages;
                    }

                    foreach (var dep in depLabels)
                    {
                        packages.Add(dep);
                    }
                }
            }

            return tfms;
        }
    }

    public class BazelEnvironment
    {
        public static string WorkingDirectory = Environment.GetEnvironmentVariable("BUILD_WORKING_DIRECTORY")!;
        public static string WorkspaceDirectory = Environment.GetEnvironmentVariable("BUILD_WORKSPACE_DIRECTORY");

        public void ResolveLabel(Label label)
        {
            label.PathRoot = label.IsRooted ? WorkspaceDirectory : WorkingDirectory;
            if (label.IsPath)
            {
                label.Filepath = Path.IsPathRooted(label.RawValue) ? label.RawValue : Path.Combine(WorkingDirectory, label.RawValue);
            }
            else
            {
                label.Filepath = Path.Combine(label.PathRoot, label.Package, label.Name);
            }
        }
    }
}