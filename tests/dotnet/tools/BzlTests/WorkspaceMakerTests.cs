using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Permissions;
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

        private const string WorkspaceTemplate = "WORKSPACE_TEMPLATE";

        [Theory]
        [MemberData(nameof(GetWorkspaces), DisableDiscoveryEnumeration = true)]
        public void WorkspaceInit(string workspaceName)
        {
            _testDir = BazelEnvironment.GetTmpDir($"{nameof(WorkspaceInit)}_{workspaceName}");
            var specs = CollectSpecs(workspaceName);
            var maker = new WorkspaceMaker(_runfiles.Runfiles, _testDir, workspaceName,
                "rules_msbuild/tests/dotnet/tools/BzlTests/WORKSPACE.FAKE.tpl");

            maker.Init();

            foreach (var spec in specs)
            {
                var info = new FileInfo(Path.Combine(_testDir, spec.Rel));
                info.Exists.Should().BeTrue($"`{spec.Rel}` should have been created.");
                var contents = File.ReadAllText(info.FullName).Replace("\r", "").Trim();
                foreach (var regex in spec.Regexes)
                {
                    regex.IsMatch(contents).Should().BeTrue($"`{regex}` should have matched:\n```\n{contents}\n```");
                }

                if (!string.IsNullOrEmpty(spec.Contents))
                {
                    contents.Should().Be(spec.Contents);
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
                            spec.Contents = File.ReadAllText(path).Replace("\r", "").Trim();
                            break;
                        case ".match":
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
        public string Contents { get; set; }
    }
}