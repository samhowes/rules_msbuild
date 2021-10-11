using System.Collections.Generic;
using System.IO;
using System.Linq;
using RulesMSBuild.Tools.Bazel;

namespace Bzl
{
    public class Template
    {
        public string Destination { get; }
        public string Contents { get; set; }

        public Template(string destination, string contents)
        {
            Destination = destination;
            Contents = contents;
        }
    }
    
    public class Templates
    {
        public Template BazelProps { get; set; }
        public Template BazelTargets { get; set; }
        public Template Workspace { get; set; }
        public Template RootBuild { get; set; }
        public Template DirectoryProps { get; set; }
        public Template DirectoryTargets { get; set; }
        public Template SolutionProps { get; set; }
        public Template SolutionTargets { get; set; }

        public IEnumerable<Template> XmlMerge => new[]
        {
            DirectoryProps,
            DirectoryTargets,
            SolutionProps,
            SolutionTargets
        };

        public IEnumerable<Template> Overwrite => new[] {BazelProps, BazelTargets};

        public static Templates CreateDefault(Runfiles runfiles)
        {
            var templates = new Templates();

            var toLoad = new (string, string, string)[]
            {
                (":WORKSPACE.tpl", "WORKSPACE", nameof(Workspace)),
                (":BUILD.root.tpl.bazel", "BUILD.bazel", nameof(RootBuild)),
                ("//extras/ide:Bazel.props", null, nameof(BazelProps)),
                ("//extras/ide:Bazel.targets", null, nameof(BazelTargets)),
                ("//extras/ide:Directory.Build.props", null, nameof(DirectoryProps)),
                ("//extras/ide:Directory.Build.targets", null, nameof(DirectoryTargets)),
                ("//extras/ide:Directory.Solution.props", null, nameof(SolutionProps)),
                ("//extras/ide:Directory.Solution.targets", null, nameof(SolutionTargets)),
            };

            var properties = typeof(Templates).GetProperties().ToDictionary(p => p.Name);
            var lRunfiles = new LabelRunfiles(runfiles, "@rules_msbuild//dotnet/tools/Bzl");

            foreach (var (label, originalDest, propertyName) in toLoad)
            {
                var property = properties[propertyName];
                var location = lRunfiles.Rlocation(label);
                var contents = File.ReadAllText(location);
                var dest = originalDest;
                if (dest == null)
                {
                    dest = Path.GetFileName(location);
                }

                var template = new Template(dest, contents);
                property.SetValue(templates, template);
            }

            return templates;
        }
    }
}