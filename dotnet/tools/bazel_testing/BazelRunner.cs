using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TestRunner
{
    public class BazelResult
    {
        public string Command { get; }
        private readonly StringBuilder _out = new StringBuilder();
        private readonly StringBuilder _err = new StringBuilder();

        public BazelResult(string command)
        {
            Command = command;
        }

        public int ExitCode { get; set; } = -1;
        public bool Success { get; set; }
        public string Stdout { get; set; }
        public string Stderr { get; set; }

        public void AddOutput(string data) => _out.AppendLine(data);
        public void AddError(string data) => _err.AppendLine(data);

        public void Finish(int exitCode)
        {
            ExitCode = exitCode;
            Success = exitCode == 0;
            Stdout = _out.ToString();
            Stderr = _err.ToString();
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

        public bool Run(string command, out BazelResult result)
        {
            using var bazel = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _binaryPath,
                    Arguments = $"--output_user_root={_outputUserRoot} {command}",
                    WorkingDirectory = _workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                    
                }
            };
            var res = new BazelResult(command);
            result = res;
            try
            {
                SetEnv(bazel.StartInfo.Environment, _workingDirectory);
                bazel.Start();
                bazel.BeginOutputReadLine();
                bazel.BeginErrorReadLine();
                bazel.OutputDataReceived += (_, data) =>
                {
                    res.AddOutput(data.Data);
                    Console.WriteLine(data.Data);
                };
                bazel.ErrorDataReceived += (_, data) =>
                {
                    res.AddError(data.Data);
                    Console.Error.WriteLine(data.Data);
                };
                bazel.WaitForExit();
            }
            finally
            {
                bazel.Kill(true);
            }
            res.Finish(bazel.ExitCode);
            return res.Success;
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
            TestLogger.Debug($"home: {home}");
            env["HOME"] = home;
            env["DOTNET_CLI_HOME"] = home;
        }
    }
}