using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bzl;
using FluentAssertions;
using MyRulesDotnet.Tools.Bazel;
using Xunit;

namespace BzlTests
{
    public class UnitTest1 : IDisposable
    {
        private static LabelRunfiles _runfiles;
        private static string _basePath;
        private string _testDir;

        public UnitTest1()
        {
        }
        
        [Theory]
        [MemberData(nameof(GetWorkspaces), DisableDiscoveryEnumeration = true)]
        public void WorkspaceInit(string workspaceName)
        {
            _testDir = BazelEnvironment.GetTmpDir($"{nameof(WorkspaceInit)}_{workspaceName}");
            var specs = CollectSpecs(workspaceName);
            var maker = new WorkspaceMaker(_testDir, workspaceName);
            maker.Init();

            foreach (var spec in specs)
            {
                var info = new FileInfo(Path.Combine(_testDir, spec.Rel));
                info.Exists.Should().BeTrue($"`{spec.Rel}` should have been created.");
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
                    if (path.EndsWith(".out"))
                    {
                        specs.Add(new FileSpec()
                        {
                            Rel = relative[..^(".out".Length)]
                        });
                    }
                    else
                    {
                        File.Copy(path, dest, true);
                    }
                }

                return true;
            });
            return specs;
        }

        public static IEnumerable<object[]> GetWorkspaces()
        {
            _runfiles = Runfiles.Create<UnitTest1>();
            var testdata = _runfiles.Rlocation("//tests/dotnet/tools/BzlTests:testdata");
            _basePath = testdata;
            var testcases = Directory.EnumerateDirectories(testdata);
            return testcases.Select(d => new object[]
            {
                d.Split(Path.DirectorySeparatorChar).Last()
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
    }
}