#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RulesMSBuild.Tools.Bazel;
using static TestRunner.TestLogger;

namespace TestRunner
{
    public class TestConfig
    {
        public string WorkspaceRoot { get; set; } = null!;
        public string ReleaseTar { get; set; } = null!;
        public string Bazel { get; set; } = null!;
        public List<string> Commands { get; set; } = null!;
        public Dictionary<string,string>? Run { get; set; }
    }

    public static class Program
    {
        static int Main(string[] args)
        {
            // for testing building an executable in an external workspace
            if (args[0] == "ping")
            {
                Console.WriteLine("pong");
                return 0;
            }
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
            var exitCode = testRunner.Run();
            Debug($"testRunner exit code is {exitCode}");
            return exitCode;
        }

    }
}
