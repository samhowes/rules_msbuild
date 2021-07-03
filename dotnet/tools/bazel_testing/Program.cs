using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
        public string WorkspaceRoot { get; set; }
        public string ReleaseTar { get; set; }
        public string Bazel { get; set; }
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

            config!.WorkspaceRoot = runfiles.Rlocation(config.WorkspaceRoot);
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
        private List<string> _cleanup = new List<string>();

        public TestRunner(Runfiles runfiles, TestConfig config)
        {
            _runfiles = runfiles;
            _config = config;
        }


        public int Run()
        {
            var testTmpDir = BazelEnvironment.GetTmpDir();
            var execRootIndex = testTmpDir.IndexOf("/execroot/", StringComparison.OrdinalIgnoreCase);
            if (execRootIndex < 0) throw new Exception($"Bad tmpdir: {testTmpDir}");
            var outputBaseDir = testTmpDir[..execRootIndex];
            var outputUserRoot = outputBaseDir; // Path.GetDirectoryName(outputBaseDir);
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
            Files.Walk(_config.WorkspaceRoot, (path, isDirectory) =>
            {
                var rel = path[(_config.WorkspaceRoot.Length + 1)..];
                var dest = Path.Combine(testDir, rel);
                if (isDirectory)
                {
                    Directory.CreateDirectory(dest);
                    return true;
                }

                Debug(rel);
                File.Copy(path, dest);
                return true;
            });

            var originalWorkspace = File.ReadAllText("WORKSPACE");
            var workspaceMaker = new WorkspaceMaker(_runfiles, testDir, workspaceName);
            workspaceMaker.Init(true,true);

            var workspace = File.ReadAllText("WORKSPACE");
            workspace = Regex.Replace(workspace, @"http_archive\(.*\n\s+name = ""rules_msbuild"",\n.*\n\s+(?<urls>.*)",
                match => match.Value.Replace(match.Groups["urls"].Value, $"urls = [\"file:{_config.ReleaseTar}\"],"));
            File.WriteAllText("WORKSPACE", workspace + originalWorkspace);

            using var bazel = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _config.Bazel,
                    Arguments = $"--output_user_root={outputUserRoot} build //...", 
                    WorkingDirectory = testDir
                }
            };

            SetEnv(bazel.StartInfo.Environment, testDir);
            bazel.Start();
            bazel.WaitForExit();

            return bazel.ExitCode;
        }

        private static void SetEnv(IDictionary<string,string> env, string workspaceRoot)
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
}
