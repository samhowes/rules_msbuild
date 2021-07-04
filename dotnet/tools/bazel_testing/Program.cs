#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using RulesMSBuild.Tools.Bazel;
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
            using var bazel = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _binaryPath,
                    Arguments = $"--output_user_root={_outputUserRoot} {command}",
                    WorkingDirectory = _workingDirectory
                }
            };
            try
            {
                exitCode = -1;

                SetEnv(bazel.StartInfo.Environment, _workingDirectory);
                bazel.Start();
                bazel.WaitForExit();
                exitCode = bazel.ExitCode;
                if (bazel.ExitCode != 0)
                    return false;
                return true;

            }
            finally
            {
                bazel.Kill(true);
            }
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
