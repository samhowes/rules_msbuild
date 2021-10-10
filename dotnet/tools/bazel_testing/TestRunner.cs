using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Bzl;
using RulesMSBuild.Tools.Bazel;
using RulesMSBuild.Tools.Builder;
using static TestRunner.TestLogger;
using Files = Bzl.Files;

namespace TestRunner
{
    public class TestRunner : IDisposable
    {
        private readonly Runfiles _runfiles;
        private readonly TestConfig _config;
        private readonly List<string> _cleanup = new List<string>();
        private BazelRunner _bazel;

        public TestRunner(Runfiles runfiles, TestConfig config)
        {
            _runfiles = runfiles;
            _config = config;
        }

        public int Run()
        {
            var testTmpDir = BazelEnvironment.GetTmpDir();
            Info($"Creating test directory with {testTmpDir}");
            // assumes we're not in a sandbox i.e. tags = ["local"]
            var execRootIndex = testTmpDir.IndexOf("/execroot/", StringComparison.OrdinalIgnoreCase);
            if (execRootIndex < 0) throw new Exception($"Bad tmpdir: {testTmpDir}");
            var outputBaseDir = testTmpDir[..execRootIndex];
            var outputUserRoot = Path.GetDirectoryName(outputBaseDir);
            var cacheDir = Path.Join(outputBaseDir, "bazel_testing");
            var execDir = Path.Join(cacheDir, "bazel_dotnet_test");
            if (Directory.Exists(execDir))
            {
                DeleteDirectory(execDir);
            }
            _cleanup.Add(execDir);

            var workspaceName = PosixPath.GetFileName(_config.WorkspaceRoot);
            var testDir = Path.Combine(execDir, "main");

            Info($"Using workspace root: {testDir}");
            Info($"Using output user root: {outputUserRoot}");

            Directory.CreateDirectory(testDir);
            Directory.SetCurrentDirectory(testDir);

            var sourceWorkspaceRpath = PosixPath.Combine(_config.WorkspaceRoot, "WORKSPACE");
            var sourceWorkspace = _runfiles.Rlocation(sourceWorkspaceRpath);
            if (sourceWorkspace == null)
                throw new Exception($"Could not find workspace file with runfile path " +
                                    $"{sourceWorkspaceRpath}, source test workspace must include a WORKSPACE file.");

            var workspaceRoot = PosixPath.GetDirectoryName(sourceWorkspace)!;
            var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in _runfiles.ListRunfiles(_config.WorkspaceRoot))
            {
                var rel = path[(workspaceRoot.Length + 1)..];
                var dest = PosixPath.Combine(testDir, rel);
                var directoryName = PosixPath.GetDirectoryName(dest)!;
                if (!directories.Contains(directoryName) && !Directory.Exists(directoryName))
                    Directory.CreateDirectory(directoryName);
                directories.Add(directoryName);
                File.Copy(path, dest);
                Debug(dest);
            }

            Info("Initializing workspace...");
            var workspaceMaker = new WorkspaceMaker(_runfiles, testDir, workspaceName, _config.WorkspaceTpl);

            _bazel = new BazelRunner(_config.Bazel, outputUserRoot!, testDir);

            var originalWorkspace = File.ReadAllText("WORKSPACE");
            var commandIndex = 0;
            if (_config.Commands[0] == "init")
            {
                commandIndex++;
                workspaceMaker.Init(true);
                UpdateWorkspaceForLocal(originalWorkspace);

                if (!_bazel!.Run("run //:gazelle", out var result))
                    return result.ExitCode;
                // this is cheating, but oh well
                File.WriteAllText("WORKSPACE",
                    File.ReadAllText("WORKSPACE")
                    + "\nload(\"deps/nuget.bzl\", \"nuget_deps\")\nnuget_deps()\n");
            }
            else
            {
                workspaceMaker.Init(true, true);
                UpdateWorkspaceForLocal(originalWorkspace);
            }

            foreach (var command in _config.Commands.Skip(commandIndex))
            {
                Info($"Executing test command '{command}'");
                if (!_bazel!.Run(command, out var result))
                {
                    Info("Detected a test failure");
                    return result.ExitCode;
                }
                    
            }

            if (_config.Run == null) return 0;
            bool hasFailure = false;
            foreach (var (command, expectedOutput) in _config.Run)
            {
                if (!_bazel!.Run($"run {command}", out var result))
                    return result.ExitCode;
                var expected = expectedOutput.Replace("\r", "");
                var actual = result.Stdout.Replace("\r", "");
                if (actual != expected)
                {
                    Console.WriteLine($"Incorrect output from: `bazel {result.Command}`\nexpected: '{expectedOutput}'\nactual: '{result.Stdout}'");
                    hasFailure = true;
                }
            }
            
            return hasFailure ? 1 : 0;
        }

        private void UpdateWorkspaceForLocal(string originalWorkspace)
        {
            var workspace = File.ReadAllText("WORKSPACE");
            bool replaced = false;
            workspace = Regex.Replace(workspace, @"http_archive\(.*\n\s+name = ""rules_msbuild"",\n.*\n\s+(?<urls>.*)",
                match =>
                {
                    replaced = true;
                    return match.Value.Replace(match.Groups["urls"].Value, $"urls = [\"file:{_config.ReleaseTar}\"],");
                });
            if (!replaced)
                throw new Exception("Failed to replace url in workspace");

            File.WriteAllText("WORKSPACE", workspace + originalWorkspace);
        }

        public void Dispose()
        {
            // return;
            var cwd = Directory.GetCurrentDirectory();
            foreach (var path in _cleanup.Select(Path.GetFullPath))
            {
                if (cwd.StartsWith(path))
                {
                    Debug($"Backing out of {cwd} to delete {path}");
                    cwd = Path.GetDirectoryName(path)!;
                    Directory.SetCurrentDirectory(cwd!); // whatever
                    Debug($"new cwd: {Directory.GetCurrentDirectory()}");
                }

                if (Directory.Exists(path))
                {
                    DeleteDirectory(path);
                }
            }
        }

        private void DeleteDirectory(string path)
        {
            Files.PostOrderWalk(path, (directory) =>
            {
                // dotnet doesn't handle bazel's symlinks well, so we manually recurse directories, but do not
                // traverse into symlinks
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    return false;
                return true;
            }, (i) =>
            {
                Debug($"Deleting {i.FullName}");
                i.Delete();
            });
        }
    }
}
