#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bzl;
using RulesMSBuild.Tools.Bazel;
using RulesMSBuild.Tools.Builder;
using static TestRunner.TestLogger;

namespace TestRunner
{
    public class TestConfig
    {
        public string WorkspaceRoot { get; set; } = null!;
        public string ReleaseTar { get; set; } = null!;
        public string WorkspaceTpl { get; set; } = null!;
        public string Bazel { get; set; } = null!;
        public List<string> Commands { get; set; } = null!;
    }

    public static class PosixPath
    {
        public const char Separator = '/';
        public static string? GetDirectoryName(string path)
        {
            var parts = path.Split(Separator);
            if (parts.Length == 1) return null;
            return string.Join(Separator, parts[..^1]);
        }

        public static string Combine(params string[] parts)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];

                if (i > 0 && part.StartsWith(Separator))
                    part = part[1..];

                builder.Append(part);
                if (i < parts.Length -1 && !part.EndsWith(Separator))
                    builder.Append(Separator);
            }

            return builder.ToString();
        }
    }
    
    public static class Program
    {
        static int Main(string[] args)
        {
            Debug(string.Join(" ", args));
            Debug(Environment.CurrentDirectory);
            if (args.Length != 1)
                return Fail($"Expected config file as only argument. Got {String.Join(" ", args)}");

            var runfiles = Runfiles.Create();

            var config = JsonSerializer.Deserialize<TestConfig>(File.ReadAllText(args[0]), new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            config!.ReleaseTar = runfiles.Rlocation(config.ReleaseTar);
            config!.Bazel = runfiles.Rlocation(config.Bazel);

            Info($"Using release tar {config!.ReleaseTar}");
            Info($"Using input workspace {config!.WorkspaceRoot}");

            using var testRunner = new TestRunner(runfiles, config);
            return testRunner.Run();
        }

    }
    public class TestRunner : IDisposable
    {
        private readonly Runfiles _runfiles;
        private readonly TestConfig _config;
        private readonly List<string> _cleanup = new List<string>();

        public TestRunner(Runfiles runfiles, TestConfig config)
        {
            _runfiles = runfiles;
            _config = config;
        }

        public int Run()
        {
            var testTmpDir = BazelEnvironment.GetTmpDir();
            // assumes we're not in a sandbox i.e. tags = ["local"]
            var execRootIndex = testTmpDir.IndexOf("/execroot/", StringComparison.OrdinalIgnoreCase);
            if (execRootIndex < 0) throw new Exception($"Bad tmpdir: {testTmpDir}");
            var outputBaseDir = testTmpDir[..execRootIndex];
            var outputUserRoot = Path.GetDirectoryName(outputBaseDir);
            var cacheDir = Path.Join(outputBaseDir, "bazel_testing");
            var execDir = Path.Join(cacheDir, "bazel_dotnet_test");
            if (Directory.Exists(execDir))
                Directory.Delete(execDir, true);
            _cleanup.Add(execDir);

            var workspaceName = Path.GetFileName(_config.WorkspaceRoot);
            var testDir = Path.Combine(execDir, workspaceName!);

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

            var workspaceMaker = new WorkspaceMaker(_runfiles, testDir, workspaceName, _config.WorkspaceTpl);
            
            var bazel = new BazelRunner(_config.Bazel, outputUserRoot!, testDir);

            var originalWorkspace = File.ReadAllText("WORKSPACE");
            var commandIndex = 0;
            if (_config.Commands[0] == "init")
            {
                commandIndex++;
                workspaceMaker.Init(true);
                UpdateWorkspaceForLocal(originalWorkspace);
                
                if (!bazel.Run("run //:gazelle", out var exitCode))
                    return exitCode;
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
                if (!bazel.Run(command, out var exitCode))
                    return exitCode;
            }

            return 0;
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
            if (DebugEnabled) return;
            foreach (var path in _cleanup)
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);    
            }

        }
    }

    public class BazelRunner
    {
        private readonly string _binaryPath;
        private readonly string _outputUserRoot;
        private readonly string _workingDirectory;

        public BazelRunner(string binaryPath, string outputUserRoot, string workingDirectory)
        {
            _binaryPath = binaryPath;
            _outputUserRoot = outputUserRoot;
            _workingDirectory = workingDirectory;
        }

        public bool Run(string command, out int exitCode)
        {
            exitCode = -1;
            using var bazel = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _binaryPath,
                    Arguments = $"--output_user_root={_outputUserRoot} {command}", 
                    WorkingDirectory = _workingDirectory
                }
            };

            SetEnv(bazel.StartInfo.Environment, _workingDirectory);
            bazel.Start();
            bazel.WaitForExit();
            exitCode = bazel.ExitCode;
            if (bazel.ExitCode != 0)
                return false;
            return true;
        }
        
        private static void SetEnv(IDictionary<string,string?> env, string workspaceRoot)
        {
            // credit to rules_nodejs: https://github.com/bazelbuild/rules_nodejs/blob/stable/internal/bazel_integration_test/test_runner.js#L356
            var bazelKeys = new List<string>()
            {
                "_RLOCATION_ISABS_PATTERN",
                "BASH_FUNC_is_absolute%%",
                "BASH_FUNC_rlocation%%",
                "BASH_FUNC_runfiles_export_envvars%%",
                "BAZEL_NODE_MODULES_ROOTS",
                "BAZEL_NODE_PATCH_REQUIRE",
                "BAZEL_NODE_RUNFILES_HELPER",
                "BAZEL_PATCH_ROOTS",
                "BAZEL_TARGET",
                "BAZEL_WORKSPACE",
                "BAZELISK_SKIP_WRAPPER",
                "BUILD_WORKING_DIRECTORY",
                "BUILD_WORKSPACE_DIRECTORY",
                "GTEST_TMP_DIR",
                "INIT_CWD",
                "JAVA_RUNFILES",
                "NODE_REPOSITORY_ARGS",
                "OLDPWD",
                "PYTHON_RUNFILES",
                "RUN_UNDER_RUNFILES",
                "RUNFILES_DIR",
                "RUNFILES",
                "TEST_BINARY",
                "TEST_INFRASTRUCTURE_FAILURE_FILE",
                "TEST_LOGSPLITTER_OUTPUT_FILE",
                "TEST_PREMATURE_EXIT_FILE",
                "TEST_SIZE",
                "TEST_SRCDIR",
                "TEST_TARGET",
                "TEST_TIMEOUT",
                "TEST_TMPDIR",
                "TEST_UNDECLARED_OUTPUTS_ANNOTATIONS_DIR",
                "TEST_UNDECLARED_OUTPUTS_DIR",
                "TEST_UNUSED_RUNFILES_LOG_FILE",
                "TEST_WARNINGS_OUTPUT_FILE",
                "TEST_WORKSPACE",
                "XML_OUTPUT_FILE",
            };

            foreach (var key in bazelKeys)
            {
                env.Remove(key);
            }

            env["PWD"] = workspaceRoot;

            string home;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                home = "/Users/" + Environment.UserName;
            } 
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                home = "C:/Users/" + Environment.UserName;
            }
            else
            {
                home = "/home/" + Environment.UserName;
            }
            Debug($"home: {home}");
            env["HOME"] = home;
            env["DOTNET_CLI_HOME"] = home;
        }
    }
}
