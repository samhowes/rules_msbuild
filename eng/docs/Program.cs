using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using RulesMSBuild.Tools.Bazel;

namespace docs
{
    class Program
    {
        static Regex TocRegex = new Regex("(<!-- toc:start -->)(.*?)(<!-- toc:end -->)", RegexOptions.Singleline);
        public static void Info(string message)
        {
            Console.WriteLine(message);
        }

        public static Regex LabelLinkRegex = new Regex(@"\[(?<link_text>[^\]]*?)\]\((?<label>(:|\/\/)[^\)]+?)\)",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);
        
        static void Main(string[] args)
        {
            var runfiles = Runfiles.Create<Program>();

            var root = BazelEnvironment.GetWorkspaceRoot();
            var tocList = new List<(string, string)>();

            for (var i = 0; i < args.Length; i+=2)
            {
                var label = new Label(args[i]);
                var file = args[i + 1];
                Info($"Processing: {label.RawValue} => {file}");
                var rlocation = runfiles.Rlocation($"rules_msbuild/{file}");
                var contents = File.ReadAllText(rlocation);

                var destRel = string.Join('/', "docs", Path.GetFileName(label.Package) + ".md");
                var dest = Path.GetFullPath(Path.Combine(root, destRel));

                contents = LabelLinkRegex.Replace(contents, (match) =>
                {
                    var labelLink = new Label(match.Groups["label"].Value);
                    string package;
                    if (labelLink.IsRelative)
                    {
                        package = labelLink.Package;
                    }
                    else
                    {
                        package = labelLink.Package;
                    }

                    var labelRel = string.Join('/', package, labelLink.Name);
                    var targetFile = Path.GetFullPath(Path.Combine(root, labelRel));
                    var rel = Path.GetRelativePath(Path.GetDirectoryName(dest)!, targetFile).Replace('\\', '/');
                    Info($"{labelLink.RawValue} => {rel}");
                    return $"[{match.Groups["link_text"]}]({rel})";
                });

                File.WriteAllText(dest, contents);

                var firstLine = Regex.Match(contents, @"^#\s*(?<title>.*)$", RegexOptions.Multiline);
                
                tocList.Add((destRel, firstLine.Groups["title"].Value));
            }
            
            var toc = new StringBuilder();
            foreach (var (path, title) in tocList)
            {
                toc.AppendLine($"1. [{title}]({path})");
            }

            var readmePath = Path.Combine(root, "README.md");
            var readme = File.ReadAllText(readmePath);
            File.WriteAllText(readmePath, TocRegex.Replace(readme, $"$1\n{toc.ToString()[..^1]}$3"));
        }
    }
}
