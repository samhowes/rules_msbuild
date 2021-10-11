#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RulesMSBuild.Tools.Bazel;
using NuGetParser;

namespace Bzl
{
    public class Template
    {
        public string Target { get; }
        public string DestinationPath { get; }

        public Template(string target, string destinationPath)
        {
            Target = target;
            DestinationPath = destinationPath;
        }
    }

    public class WorkspaceMaker
    {
        private const string MarkerName = "generated";
        private static class Templates
        {
            public static readonly Template Workspace = new Template(":WORKSPACE.tpl", "WORKSPACE");
            public static readonly Template RootBuild = new Template(":BUILD.root.tpl.bazel", "BUILD.bazel");
        }
        
        private readonly LabelRunfiles _runfiles;
        private readonly string _workspaceRoot;
        private readonly string _workspaceName;

        private static readonly Regex TemplateRegex = new Regex(@"@@(\w+)@@",
            RegexOptions.Compiled | RegexOptions.Multiline);

        private readonly Dictionary<string,string> _variables;
        private readonly Template _workspaceTemplate;

        public WorkspaceMaker(Runfiles runfiles, string workspaceRoot, string workspaceName, string? workspaceTemplate = null)
        {
            _runfiles = new LabelRunfiles(runfiles, new Label("rules_msbuild", "dotnet/tools/Bzl"));
            _workspaceRoot = workspaceRoot;
            _workspaceName = workspaceName;
            _workspaceTemplate = workspaceTemplate != null
                ? new Template(workspaceTemplate, Templates.Workspace.DestinationPath)
                : Templates.Workspace;
            _variables = new Dictionary<string, string>()
            {
                ["workspace_name"] = workspaceName,
                ["nuget_workspace_name"] = "nuget",
                ["bazel_bin"] = "bazel-bin",
            };
        }

        public void Init(bool force=false, bool workspaceOnly=false)
        {
            WriteWorkspace(force);

            if (!workspaceOnly)
                ExpandTemplate(Templates.RootBuild);

            if (workspaceOnly) return;
            var msbuildRoot = _workspaceRoot;
            Files.Walk(_workspaceRoot, (path, isDirectory) =>
            {
                if (isDirectory) return true;
                if (path.EndsWith(".sln") || path.EndsWith("proj"))
                {
                    msbuildRoot = Path.GetDirectoryName(path);
                    return false;
                }

                return true;
            });

            var workspacePath = msbuildRoot == _workspaceRoot ? "" : Path.GetRelativePath(msbuildRoot, _workspaceRoot) + "/";
            _variables["workspace_path"] = workspacePath;

            foreach (var src in _runfiles.ListRunfiles("//extras/ide"))
            {
                var dest = Path.Combine(msbuildRoot, Path.GetFileName(src));
                CopyTemplate(src, dest);
                ReportFile(dest);
            }
        }

        private void WriteWorkspace(bool force)
        {
            var workspaceFile = new FileInfo(Path.Combine(_workspaceRoot, "WORKSPACE"));
            BuildWriter writer;
            BuildReader? reader = null;
            Stream? temp = null;
            if (!workspaceFile.Exists || force)
            {
                writer = new BuildWriter(File.Create(workspaceFile.FullName));
                writer.Call("workspace", ("name", _workspaceName));
            }
            else
            {
                reader = new BuildReader(workspaceFile.OpenRead());
                temp = new MemoryStream();
                writer = new BuildWriter(temp);
                writer.Raw(reader.GetUntilMarker(MarkerName), false);
            }

            writer.StartMarker(MarkerName);
            writer.Raw(File.ReadAllText(_runfiles.Runfiles.Rlocation(_workspaceTemplate.Target)));
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
                using var dest = workspaceFile.Create();
                temp.CopyTo(dest);
            }
            
            writer.Dispose();
        }

        private void ExpandTemplate(Template t)
        {
            var sourcePath = t.Target.StartsWith("//") || t.Target.StartsWith(":")
                ? _runfiles.PackagePath(t.Target)
                : _runfiles.Runfiles.Rlocation(t.Target); 
            CopyTemplate(sourcePath, t.DestinationPath);
            ReportFile(t.DestinationPath);
        }

        private void ReportFile(string path)
        {
            if (Path.IsPathRooted(path))
            {
                path = path[(_workspaceRoot.Length + 1)..];
            }
            Console.WriteLine($"Created: {path}");
        }
        
        private void CopyTemplate(string templatePath, string workspaceRelativeDest)
        {
            var contents = File.ReadAllText(templatePath);
            contents = TemplateRegex.Replace(contents, (match) =>
            {
                if (_variables.TryGetValue(match.Groups[1].Value, out var replacement)) return replacement;
                return match.Value;
            });
            
            File.WriteAllText(Path.Combine(_workspaceRoot, workspaceRelativeDest), contents);
        }
    }
}
