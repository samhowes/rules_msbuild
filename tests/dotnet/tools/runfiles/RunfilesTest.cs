using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
 using MyRulesDotnet.Tools.Bazel;
using Xunit;

namespace MyRulesDotnet.Tools.RunfilesTests
{
    public static class Files
    {
        public static string CreateTempDirectory()
        {
            var path = Path.Combine(BazelEnvironment.GetTestTmpDir(), Path.GetRandomFileName());
            Directory.CreateDirectory(path);
            return path;
        }

        public static string CreateTempFile()
        {
            var testTmpdir = BazelEnvironment.GetTestTmpDir();
            return Path.Combine(testTmpdir, Path.GetRandomFileName());
        }
    }

    public class RunfilesTest
    {
        private TException AssertThrows<TException>(Action action) where TException : Exception
        {
            return action.Should().ThrowExactly<TException>().Subject.Single();
        }

        private static bool IsWindows()
        {
            return Path.DirectorySeparatorChar == '\\';
        }

        private void AssertRlocationArg(Runfiles runfiles, string path, string error)
        {
            var e =
                AssertThrows<ArgumentException>(() => runfiles.Rlocation(path));
            if (error != null) e.Message.Should().Contain(error);
        }

        [Fact]
        public void TestRlocationArgumentValidation()
        {
            var dir =
                Files.CreateTempDirectory();

            var r = Runfiles.Create(new Dictionary<string, string> {{"RUNFILES_DIR", dir}});
            AssertRlocationArg(r, null, null);
            AssertRlocationArg(r, "", null);
            AssertRlocationArg(r, "../foo", "is not normalized");
            AssertRlocationArg(r, "foo/..", "is not normalized");
            AssertRlocationArg(r, "foo/../bar", "is not normalized");
            AssertRlocationArg(r, "./foo", "is not normalized");
            AssertRlocationArg(r, "foo/.", "is not normalized");
            AssertRlocationArg(r, "foo/./bar", "is not normalized");
            AssertRlocationArg(r, "//foobar", "is not normalized");
            AssertRlocationArg(r, "foo//", "is not normalized");
            AssertRlocationArg(r, "foo//bar", "is not normalized");
            AssertRlocationArg(r, "\\foo", "path is absolute without a drive letter");
        }

        [Fact]
        public void TestCreatesManifestBasedRunfiles()
        {
            using var mf = new MockFile("a/b c/d");
            var r =
                Runfiles.Create(
                    new Dictionary<string, string>
                    {
                        {"RUNFILES_MANIFEST_ONLY", "1"},
                        {"RUNFILES_MANIFEST_FILE", mf.Path},
                        {"RUNFILES_DIR", "ignored when RUNFILES_MANIFEST_ONLY=1"},
                        {
                            "TEST_SRCDIR", "should always be ignored"
                        }
                    });
            r.Rlocation("a/b").Should().Be("c/d");
            r.Rlocation("foo").Should().BeNull();

            if (IsWindows())
            {
                r.Rlocation("c:/foo").Should().Be("c:/foo");
                r.Rlocation("c:\\foo").Should().Be("c:\\foo");
            }
            else
            {
                r.Rlocation("/foo").Should().Be("/foo");
            }
        }

        [Fact]
        public void TestCreatesDirectoryBasedRunfiles()
        {
            var dir =
                Files.CreateTempDirectory();

            var r =
                Runfiles.Create(
                    new Dictionary<string, string>
                    {
                        {"RUNFILES_MANIFEST_FILE", "ignored when RUNFILES_MANIFEST_ONLY is not set to 1"},
                        {"RUNFILES_DIR", dir},
                        {"TEST_SRCDIR", "should always be ignored"}
                    });
            r.Rlocation("a/b").Should().EndWith("/a/b");
            r.Rlocation("foo").Should().EndWith("/foo");
        }

        [Fact]
        public void TestIgnoresTestSrcdirWhenJavaRunfilesIsUndefinedAndJustFails()
        {
            var dir =
                Files.CreateTempDirectory();

            Action action = () => Runfiles.Create(
                new Dictionary<string, string>
                {
                    {"RUNFILES_DIR", dir},
                    {"RUNFILES_MANIFEST_FILE", "ignored when RUNFILES_MANIFEST_ONLY is not set to 1"},
                    {
                        "TEST_SRCDIR", "should always be ignored"
                    }
                });

            action.Should().NotThrow();

            var e =
                AssertThrows<IOException>(
                    () =>
                        Runfiles.Create(
                            new Dictionary<string, string>
                            {
                                {"RUNFILES_DIR", ""},
                                {"JAVA_RUNFILES", ""},
                                {
                                    "RUNFILES_MANIFEST_FILE",
                                    "ignored when RUNFILES_MANIFEST_ONLY is not set to 1"
                                },
                                {"TEST_SRCDIR", "should always be ignored"}
                            }));
            e.Message.Should().Contain("$RUNFILES_DIR");
        }

        [Fact]
        public void TestFailsToCreateManifestBasedBecauseManifestDoesNotExist()
        {
            var e = AssertThrows<FileNotFoundException>(
                () => Runfiles.Create(new Dictionary<string, string>
                {
                    {"RUNFILES_MANIFEST_ONLY", "1"},
                    {"RUNFILES_MANIFEST_FILE", "non-existing path"}
                }));
            e.Message.Should().Contain("non-existing path");
        }

        [Fact]
        public void TestManifestBasedEnvVars()
        {
            var dir = Files.CreateTempDirectory();

            var mf = Path.Combine(dir, "MANIFEST");
            File.WriteAllLines(mf, Array.Empty<string>(), Encoding.UTF8);

            var envvars =
                Runfiles.Create(
                        new Dictionary<string, string>
                        {
                            {"RUNFILES_MANIFEST_ONLY", "1"},
                            {"RUNFILES_MANIFEST_FILE", mf},
                            {"RUNFILES_DIR", "ignored when RUNFILES_MANIFEST_ONLY=1"},
                            {"JAVA_RUNFILES", "ignored when RUNFILES_DIR has a value"},
                            {"TEST_SRCDIR", "should always be ignored"}
                        })
                    .GetEnvVars();
            envvars.Keys.Should().Equal("RUNFILES_MANIFEST_ONLY", "RUNFILES_MANIFEST_FILE", "RUNFILES_DIR");

            envvars["RUNFILES_MANIFEST_ONLY"].Should().Be("1");
            envvars["RUNFILES_MANIFEST_FILE"].Should().Be(mf);
            envvars["RUNFILES_DIR"].Should().Be(dir);
            var rfDir = Path.Combine(dir, "foo.runfiles");
            Directory.CreateDirectory(rfDir);
            mf = Path.Combine(dir, "foo.runfiles_manifest");
            File.WriteAllLines(mf, Array.Empty<string>(), Encoding.UTF8);
            envvars =
                Runfiles.Create(
                        new Dictionary<string, string>
                        {
                            {"RUNFILES_MANIFEST_ONLY", "1"},
                            {"RUNFILES_MANIFEST_FILE", mf},
                            {"RUNFILES_DIR", "ignored when RUNFILES_MANIFEST_ONLY=1"},
                            {"TEST_SRCDIR", "should always be ignored"}
                        })
                    .GetEnvVars();
            envvars["RUNFILES_MANIFEST_ONLY"].Should().Be("1");
            envvars["RUNFILES_MANIFEST_FILE"].Should().Be(mf);
            envvars["RUNFILES_DIR"].Should().Be(rfDir);
        }

        [Fact]
        public void TestDirectoryBasedEnvVars()
        {
            var dir =
                Files.CreateTempDirectory();

            var envVars =
                Runfiles.Create(
                        new Dictionary<string, string>
                        {
                            {
                                "RUNFILES_MANIFEST_FILE",
                                "ignored when RUNFILES_MANIFEST_ONLY is not set to 1"
                            },
                            {
                                "RUNFILES_DIR",
                                dir
                            },
                            {
                                "JAVA_RUNFILES",
                                "ignored when RUNFILES_DIR has a value"
                            },
                            {
                                "TEST_SRCDIR",
                                "should always be ignored"
                            }
                        })
                    .GetEnvVars();
            envVars.Keys.Should().Equal("RUNFILES_DIR");
            envVars["RUNFILES_DIR"].Should().Be(dir);
        }

        [Fact]
        public void TestDirectoryBasedRlocation()
        {
            // The DirectoryBased implementation simply joins the runfiles directory and the runfile's path
            // on a "/". DirectoryBased does not perform any normalization, nor does it check that the path
            // exists.
            var dir = Path.Combine(BazelEnvironment.GetTestTmpDir()!, "mock/runfiles");
            Directory.CreateDirectory(dir).Exists.Should().Be(true);

            var r = Runfiles.CreateDirectoryBasedForTesting(dir);
            // Escaping for "\": once for string and once for regex.
            r.Rlocation("arg").Should().MatchRegex(@".*[/\\]mock[/\\]runfiles[/\\]arg");
        }

        [Fact]
        public void TestManifestBasedRlocation()
        {
            using var mf = new MockFile(
                "Foo/runfile1 C:/Actual Path\\runfile1",
                "Foo/Bar/runfile2 D:\\the path\\run file 2.txt");

            var r = Runfiles.CreateManifestBasedForTesting(mf.Path);
            r.Rlocation("Foo/runfile1").Should().Be("C:/Actual Path\\runfile1");
            r.Rlocation("Foo/Bar/runfile2").Should().Be("D:\\the path\\run file 2.txt");
            r.Rlocation("unknown").Should().BeNull();
        }

        [Fact]
        public void TestDirectoryBasedCtorArgumentValidation()
        {
            AssertThrows<ArgumentException>(
                () => Runfiles.CreateDirectoryBasedForTesting(null));

            AssertThrows<ArgumentException>(() => Runfiles.CreateDirectoryBasedForTesting(""));

            AssertThrows<ArgumentException>(
                () => Runfiles.CreateDirectoryBasedForTesting("non-existent directory is bad"));

            Runfiles.CreateDirectoryBasedForTesting(BazelEnvironment.GetTestTmpDir());
        }

        [Fact]
        public void TestManifestBasedCtorArgumentValidation()
        {
            AssertThrows<ArgumentException>(() => Runfiles.CreateManifestBasedForTesting(null));
            AssertThrows<ArgumentException>(() => Runfiles.CreateManifestBasedForTesting(""));

            using var mf = new MockFile("a b");
            Runfiles.CreateManifestBasedForTesting(mf.Path);
        }
    }
}
