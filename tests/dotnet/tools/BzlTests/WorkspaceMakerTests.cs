using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Bzl;
using FluentAssertions;
using RulesMSBuild.Tools.Bazel;
using Xunit;

namespace BzlTests
{
    public class WorkspaceMakerTests : IDisposable
    {
        private static LabelRunfiles _runfiles;
        private static string _basePath;
        private string _testDir;

        [Theory]
        [MemberData(nameof(GetWorkspaces), DisableDiscoveryEnumeration = true)]
        public void WorkspaceInit(string workspaceName)
        {
            _testDir = BazelEnvironment.GetTmpDir($"{nameof(WorkspaceInit)}_{workspaceName}");
            var specs = CollectSpecs(workspaceName);

            var templates = new Templates()
            {
                Workspace = new Template("WORKSPACE", "FAKE_WORKSPACE"),
                RootBuild = new Template("BUILD.bazel", "FAKE_ROOT_BUILD"),
                BazelProps = new Template("Bazel.props", "FAKE_BAZEL_PROPS"),
                BazelTargets = new Template("Bazel.targets", "FAKE_BAZEL_TARGETS"),
                DirectoryProps = new Template("Directory.Build.props", "FAKE_DIRECTORY_PROPS"),
                DirectoryTargets = new Template("Directory.Build.targets", "FAKE_DIRECTORY_TARGETS"),
                SolutionProps = new Template("Directory.Solution.props", "FAKE_SOLUTION_PROPS"),
                SolutionTargets = new Template("Directory.Solution.targets", "FAKE_SOLUTION_TARGETS"),
            };

            foreach (var template in templates.XmlMerge)
            {
                template.Contents = $"<Project>\n    {template.Contents}\n</Project>\n";
            }
            
            var maker = new WorkspaceMaker(_testDir, workspaceName, templates);

            maker.Init();

            foreach (var spec in specs)
            {
                var info = new FileInfo(Path.Combine(_testDir, spec.Rel));
                info.Exists.Should().BeTrue($"`{spec.Rel}` should have been created.");
                var contents = File.ReadAllLines(info.FullName);
                // var joined = string.Join("\n", contents);
                // foreach (var regex in spec.Regexes)
                // {
                //     regex.IsMatch(joined).Should().BeTrue($"`{regex}` should have matched:\n```\n{joined}\n```");
                // }

                if (spec.Contents.Length > 0 )
                {
                    var builder = new StringBuilder().AppendLine(spec.Rel);

                    var expectedIndex = 0;
                    var actualIndex = 0;

                    var actual = contents;
                    var expected = spec.Contents;
                    var failed = false;
                    for (;expectedIndex < expected.Length && actualIndex < actual.Length;)
                    {
                        var e = expected[expectedIndex];
                        var a = actual[actualIndex];
                        if (string.CompareOrdinal(e, a) == 0)
                        {
                            builder.Append("   ");
                            builder.AppendLine(e);
                        }
                        else
                        {
                            failed = true;
                            builder.Append(" - ");
                            builder.AppendLine(e);
                            builder.Append(" + ");
                            builder.AppendLine(a);    
                        }
                        
                        expectedIndex++;
                        actualIndex++;
                    }

                    failed.Should().Be(false, builder.ToString());
                }
            }
        }

        private List<FileSpec> CollectSpecs(string workspaceName)
        {
            var specs = new List<FileSpec>();

            var input = Path.Combine(_basePath, workspaceName);
            Files.Walk(input, (path, isDirectory) =>
            {
                var relative = path[(input.Length + 1)..];
                var dest = Path.Combine(_testDir, relative);
                if (isDirectory)
                {
                    Directory.CreateDirectory(dest);
                }
                else
                {
                    var extension = Path.GetExtension(path).ToLower();
                    var spec = new FileSpec();

                    switch (extension)
                    {
                        case ".out":
                            spec.Contents = File.ReadAllLines(path);
                            break;
                        case ".match":
                            spec.Contents = Array.Empty<string>();
                            spec.Regexes = File.ReadAllLines(path).Select(l =>
                                {
                                    if (!l.StartsWith("%"))
                                    {
                                        l = Regex.Escape(l);
                                    }
                                    else
                                    {
                                        l = l[1..];
                                    }

                                    return new Regex(l, RegexOptions.Multiline | RegexOptions.IgnoreCase);
                                })
                                .ToList();
                            break;
                        case ".in":
                            dest = dest[..^3];
                            goto default;
                        default:
                            File.Copy(path, dest, true);
                            return true;
                    }

                    spec.Rel = relative[..^extension.Length];
                    specs.Add(spec);
                }

                return true;
            });
            return specs;
        }

        public static IEnumerable<object[]> GetWorkspaces()
        {
            _runfiles = Runfiles.Create<WorkspaceMakerTests>();

            var list = _runfiles.ListRunfiles("//tests/dotnet/tools/BzlTests:testdata").ToList();
            var first = list.First();
            _basePath = first[..(first.IndexOf("testdata", StringComparison.Ordinal) + "testdata".Length)];

            return list
                .Select(d => d[(_basePath.Length + 1)..].Split(Runfiles.PathSeparator, 2)[0])
                .Distinct()
                .Select(d => new object[]
                {
                    d
                }).ToArray();
        }

        public void Dispose()
        {
            if (_testDir != null)
            {
                Directory.Delete(_testDir, true);
            }
        }
    }

    public class FileSpec
    {
        public string Rel { get; set; }
        public List<Regex> Regexes { get; set; } = new List<Regex>();
        public string[] Contents { get; set; }
    }
}