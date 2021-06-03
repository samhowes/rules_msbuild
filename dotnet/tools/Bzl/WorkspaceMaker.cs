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

        public WorkspaceMaker(Runfiles runfiles, string workspaceRoot, string workspaceName)
        {
            _runfiles = new LabelRunfiles(runfiles, new Label("my_rules_dotnet", "dotnet/tools/Bzl"));
            _workspaceRoot = workspaceRoot;
            _workspaceName = workspaceName;
        }

        public void Init()
        {
            var workspaceFile = new FileInfo(Path.Combine(_workspaceRoot, "WORKSPACE"));
            if (!workspaceFile.Exists)
            {
                var variables = new Dictionary<string, string>() {["workspace_name"] = _workspaceName};
                foreach (var (templateName, name) in new []
                {
                    (":BUILD.root.tpl.bazel", "BUILD.bazel"), 
                    (":WORKSPACE.tpl", "WORKSPACE")
                })
                {
                    var contents = File.ReadAllText(_runfiles.PackagePath(templateName));
                    contents = TemplateRegex.Replace(contents, (match) =>
                    {
                        if (variables.TryGetValue(match.Groups[1].Value, out var replacement)) return replacement;
                        return match.Value;
                    });
                    
                    File.WriteAllText(Path.Combine(_workspaceRoot, name), contents);
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

            foreach (var file in new []
            {
                "Directory.Build.props",
                "Directory.Build.targets",
                "Directory.Solution.props",
                "Directory.Solution.targets",
                "Bazel.props",
            })
            {
                using var _ = File.Create(Path.Combine(msbuildRoot, file));
            }
        }
    }
}