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
    class TestConfig
    {
        public string WorkspaceRoot { get; set; }
        public string ReleaseTar { get; set; }
        public string Bazel { get; set; }
    }
    class Program
    {
        static int Main(string[] args)
        {
            Debug(string.Join(" ", args));
            Debug(Environment.CurrentDirectory);
            if (args.Length != 1)
                return Fail($"Expected config file as only argument. Got {args}");

            var runfiles = Runfiles.Create();
            
            var config = JsonSerializer.Deserialize<TestConfig>(File.ReadAllText(args[0]), new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            var tmpDir = BazelEnvironment.GetTmpDir(config!.WorkspaceRoot);
            
            config!.WorkspaceRoot = runfiles.Rlocation(config.WorkspaceRoot);
            config!.ReleaseTar = runfiles.Rlocation(config.ReleaseTar);
            config!.Bazel = runfiles.Rlocation(config.Bazel);
            
            Info($"Using release tar {config!.ReleaseTar}");
            Info($"Using input workspace {config!.WorkspaceRoot}");
            Info($"Using tmp workspace {tmpDir}");

            try
            {
                return RunTest(runfiles, tmpDir, config);
            }
            finally
            {
                if (!DebugEnabled)
                    if (Directory.Exists(tmpDir))
                        Directory.Delete(tmpDir, true);
            }
        }

        private static int RunTest(Runfiles runfiles, string tmpDir, TestConfig config)
        {
            Directory.CreateDirectory(tmpDir);
            Directory.SetCurrentDirectory(tmpDir);
            Files.Walk(config.WorkspaceRoot, (path, isDirectory) =>
            {
                var rel = path[(config.WorkspaceRoot.Length + 1)..];
                var dest = Path.Combine(tmpDir, rel);
                if (isDirectory)
                {
                    Directory.CreateDirectory(dest);
                    return true;
                }

                Debug(rel);
                File.Copy(path, dest);
                return true;
            });

            var workspaceMaker = new WorkspaceMaker(runfiles, tmpDir, tmpDir.Split(Path.DirectorySeparatorChar).Last());
            workspaceMaker.Init(true,true);

            var workspace = File.ReadAllText("WORKSPACE");
            workspace = Regex.Replace(workspace, @"http_archive\(.*\n\s+name = ""rules_msbuild"",\n.*\n\s+(?<urls>.*)",
                match => match.Value.Replace(match.Groups["urls"].Value, $"urls = [\"file:{config.ReleaseTar}\"],"));
            File.WriteAllText("WORKSPACE", workspace);

            using var bazel = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = config.Bazel,
                    Arguments = "build //...", 
                    WorkingDirectory = tmpDir
                }
            };

            SetEnv(bazel.StartInfo.Environment, tmpDir);
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
    }
}
