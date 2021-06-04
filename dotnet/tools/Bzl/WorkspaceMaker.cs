using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MyRulesDotnet.Tools.Bazel;
using NuGetParser;

namespace Bzl
{
    public class WorkspaceMaker
    {
        private readonly LabelRunfiles _runfiles;
        private readonly string _workspaceRoot;
        private readonly string _workspaceName;

        private static readonly Regex TemplateRegex = new Regex(@"@@(\w+)@@",
            RegexOptions.Compiled | RegexOptions.Multiline);

        private readonly Dictionary<string,string> _variables;

        public WorkspaceMaker(Runfiles runfiles, string workspaceRoot, string workspaceName)
        {
            _runfiles = new LabelRunfiles(runfiles, new Label("my_rules_dotnet", "dotnet/tools/Bzl"));
            _workspaceRoot = workspaceRoot;
            _workspaceName = workspaceName;
            _variables = new Dictionary<string, string>()
            {
                ["workspace_name"] = _workspaceName,
                ["nuget_workspace_name"] = "nuget",
                ["bazel_bin"] = "bazel-bin",
            };
        }

        public void Init()
        {
            var workspaceFile = new FileInfo(Path.Combine(_workspaceRoot, "WORKSPACE"));
            if (!workspaceFile.Exists)
            {
                foreach (var (templateName, name) in new []
                {
                    (":BUILD.root.tpl.bazel", "BUILD.bazel"), 
                    (":WORKSPACE.tpl", "WORKSPACE")
                })
                {
                    CopyTemplate(_runfiles.PackagePath(templateName), name);
                    ReportFile(name);
                }
            }

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
