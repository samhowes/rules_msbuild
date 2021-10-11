#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NuGetParser;

namespace Bzl
{
    public class WorkspaceMaker
    {
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
                var file = new FileInfo(Path.Combine(_workspaceRoot, template.Destination));
                var created = false;
                Stream original;
                if (!file.Exists)
                {
                    original = new MemoryStream();
                    var initial = new StreamWriter(original);
                    initial.Write("<Project>\n</Project>\n");
                    initial.Flush();
                    original.Seek(0, SeekOrigin.Begin);
                    created = true;
                }
                else
                {
                    original = file.OpenRead();
                }
                
                string replaced;
                using (original)
                {
                    var merger = new XmlMerger(original);
                    const string projectOpen = "<Project>";
                    const string projectClose = "</Project>";

                    var fragmentStart = template.Contents.IndexOf(projectOpen, StringComparison.OrdinalIgnoreCase);
                    fragmentStart = template.Contents.IndexOf('\n', fragmentStart) + 1;

                    var fragmentEnd = template.Contents.LastIndexOf(projectClose, StringComparison.OrdinalIgnoreCase);

                    var fragment = template.Contents[fragmentStart..fragmentEnd]; 
                    
                    var substituted = SubstituteVariables(fragment);
                    replaced = merger.Replace(MarkerName, substituted);
                }

                using var writer = file.CreateText();
                writer.Write(replaced);
                ReportFile(template.Destination, created);
            }

            foreach (var template in _templates.Overwrite)
            {
                var dest = Path.Combine(_workspaceRoot, template.Destination);
                File.WriteAllText(dest, SubstituteVariables(template.Contents));
                Console.WriteLine($"Overwrote: {template.Destination}");
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