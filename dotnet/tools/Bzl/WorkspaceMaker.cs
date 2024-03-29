#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NuGetParser;

namespace Bzl
{
    public class WorkspaceMaker
    {
        private static readonly Regex ReplaceRegex = new Regex(
            @"\n([^\n]+?)(bzl:generated start)(?<public>.*?)((\n([^\n]+?)(bzl:generated end[^\n]+))|$)",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private const string MarkerName = "generated";

        private readonly string _workspaceRoot;
        private readonly string _workspaceName;
        private readonly Templates _templates;

        private static readonly Regex TemplateRegex = new Regex(@"@@(\w+)@@",
            RegexOptions.Compiled | RegexOptions.Multiline);

        private readonly Dictionary<string, string> _variables;

        public WorkspaceMaker(string workspaceRoot, string workspaceName, Templates templates)
        {
            _workspaceRoot = workspaceRoot;
            _workspaceName = workspaceName;
            _templates = templates;
            _variables = new Dictionary<string, string>()
            {
                ["workspace_name"] = workspaceName,
                ["nuget_workspace_name"] = "nuget",
                ["bazel_bin"] = "bazel-bin",
            };
        }

        public void Init(bool force = false, bool workspaceOnly = false)
        {
            WriteBzl(_templates.Workspace, (writer) => writer.Call("workspace", ("name", _workspaceName)), force);

            if (!workspaceOnly)
                WriteBzl(_templates.RootBuild, (_) => { }, force);

            if (workspaceOnly) return;
            _variables["workspace_path"] = "";

            foreach (var template in _templates.XmlMerge)
            {
                const string projectOpen = "<Project>";
                const string projectClose = "</Project>";

                var fragmentStart = template.Contents.IndexOf(projectOpen, StringComparison.OrdinalIgnoreCase);
                fragmentStart = template.Contents.IndexOf('\n', fragmentStart) + 1;
                var fragmentEnd = template.Contents.LastIndexOf(projectClose, StringComparison.OrdinalIgnoreCase);
                var fragment = template.Contents[fragmentStart..fragmentEnd];
                var substituted = SubstituteVariables(fragment);

                var file = new FileInfo(Path.Combine(_workspaceRoot, template.Destination));

                var created = false;
                var builder = new StringBuilder();
                string footer;
                if (!file.Exists)
                {
                    builder.AppendLine("<Project>");
                    footer = projectClose;
                    created = true;
                }
                else
                {
                    var original = File.ReadAllText(file.FullName);
                    var match = ReplaceRegex.Match(original);
                    if (!match.Success)
                    {
                        var close = original.LastIndexOf(projectClose, StringComparison.Ordinal);
                        if (close < 0)
                            builder.AppendLine(original);
                        else
                            builder.AppendLine(original[0..close]);
                        footer = projectClose;
                    }
                    else
                    {
                        builder.AppendLine(original[0..match.Index]);
                        footer = original[(match.Index + match.Length + 1)..];
                    }
                }

                builder
                    .AppendLine($"    <!--  bzl:{MarkerName} start  -->")
                    .Append(substituted)
                    .AppendLine($"    <!--  bzl:{MarkerName} end  -->");

                builder.AppendLine(footer);

                using var writer = file.CreateText();
                writer.Write(builder.ToString());
                ReportFile(template.Destination, created);
            }

            foreach (var template in _templates.Overwrite)
            {
                var dest = Path.Combine(_workspaceRoot, template.Destination);
                File.WriteAllText(dest, SubstituteVariables(template.Contents));
                Console.WriteLine($"Overwrote: {template.Destination}");
            }

            var depsFolder = new DirectoryInfo(Path.Combine(_workspaceRoot, "deps"));
            if (!depsFolder.Exists)
                depsFolder.Create();
            var nugetDeps = new FileInfo(Path.Combine(depsFolder.FullName, "nuget.bzl"));
            if (!nugetDeps.Exists)
            {
                using var _ = nugetDeps.Create();
                ReportFile("deps/nuget.bzl", true);
            }
        }

        private void WriteBzl(Template template, Action<BuildWriter> onNotExists, bool force)
        {
            var file = new FileInfo(Path.Combine(_workspaceRoot, template.Destination));
            BuildWriter writer;
            BuildReader? reader = null;
            Stream? temp = null;
            var created = false;
            if (!file.Exists || force)
            {
                writer = new BuildWriter(File.Create(file.FullName));
                onNotExists(writer);
                created = true;
            }
            else
            {
                reader = new BuildReader(file.OpenRead());
                temp = new MemoryStream();
                writer = new BuildWriter(temp);
                writer.Raw(reader.GetUntilMarker(MarkerName), false);
            }

            writer.StartMarker(MarkerName);
            writer.Raw(template.Contents);
            writer.EndMarker(MarkerName);

            if (reader != null)
            {
                reader.SkipToEnd(MarkerName);
                writer.Raw(reader.ReadAll(), false);
                reader.Dispose();
            }

            if (temp != null)
            {
                writer.Flush();
                temp.Seek(0, SeekOrigin.Begin);
                using var dest = file.Create();
                temp.CopyTo(dest);
            }

            writer.Dispose();
            ReportFile(template.Destination, created);
        }

        private void ReportFile(string path, bool created)
        {
            if (Path.IsPathRooted(path))
            {
                path = path[(_workspaceRoot.Length + 1)..];
            }

            var text = created ? "Created" : "Updated";
            Console.WriteLine($"{text}: {path}");
        }

        private void CopyTemplate(string templatePath, string workspaceRelativeDest)
        {
            var contents = File.ReadAllText(templatePath);
            contents = SubstituteVariables(contents);

            File.WriteAllText(Path.Combine(_workspaceRoot, workspaceRelativeDest), contents);
        }

        private string SubstituteVariables(string contents)
        {
            contents = TemplateRegex.Replace(contents, (match) =>
            {
                if (_variables.TryGetValue(match.Groups[1].Value, out var replacement)) return replacement;
                return match.Value;
            });
            return contents;
        }
    }
}