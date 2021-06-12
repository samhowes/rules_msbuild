using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Bzl;
using RulesMSBuild.Tools.Bazel;
using static TestRunner.TestLogger;

namespace TestRunner
{
    class TestConfig
    {
        public string WorkspaceRoot { get; set; }
    }
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Fail($"Expected config file as only argument. Got {args}");
                return;
            }

            var runfiles = Runfiles.Create();
            var anchor = runfiles.Rlocation("rules_msbuild/dotnet/tools/bazel_testing/ANCHOR");
            var info = new FileInfo(anchor);
            Info(Path.GetFullPath(info.Name));
            
            var config = JsonSerializer.Deserialize<TestConfig>(File.ReadAllText(args[0]), new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            var tmpDir = BazelEnvironment.GetTmpDir(config!.WorkspaceRoot);
            config!.WorkspaceRoot = Path.GetFullPath(config.WorkspaceRoot);
            Info($"Using input workspace {config!.WorkspaceRoot}");
            Info($"Using tmp workspace {tmpDir}");

            try
            {
                RunTest(tmpDir, config);
            }
            finally
            {
                if (Directory.Exists(tmpDir))
                    Directory.Delete(tmpDir, true);
            }
            
        }

        private static void RunTest(string tmpDir, TestConfig config)
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
            
        }
    }
}
