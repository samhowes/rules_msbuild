#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using CommandLine;
using RulesMSBuild.Tools.Bazel;

namespace Bzl
{
    [Verb("init", isDefault: true, HelpText = "Initialize a workspace with rules_msbuild")]
    public class InitOptions
    {
        [Value(0,
            MetaName = "workspace name",
            HelpText =
                "Name of the workspace to create, will create a new directory with this name. Will initialize the" +
                "current directory if not specified.",
            Required = false)]
        public string? WorkspaceName { get; set; }
    }

    [Verb("gazelle", HelpText = "Runs gazelle to generate BUILD files for rules_msbuild in the current workspace")]
    public class GazelleOptions
    {
    }

    [Verb("restore", HelpText = "Restores msbuild projects in the workspace, useful for working with IDEs")]
    public class RestoreOptions
    {
        [Value(0, Default = "//...", HelpText = "The package expression to restore")]
        public string Package { get; set; }
    }

    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "_test")
                return Test(args[1]);

            return Parser.Default.ParseArguments<InitOptions, GazelleOptions, RestoreOptions>(args)
                .MapResult(
                    (InitOptions init) => Init(init),
                    (GazelleOptions gazelle) => Gazelle(gazelle),
                    (RestoreOptions restore) => Restore(restore),
                    errors => 1);
        }

        private static int Test(string tarPath)
        {
            var workspaceRoot = Directory.GetCurrentDirectory();
            var workspaceName = Path.GetFileName(workspaceRoot);
            var runfiles = Runfiles.Create();
            var templates = Templates.CreateDefault(runfiles);

            var workspaceContents =
                Util.UpdateWorkspaceTemplate(runfiles, tarPath, $"file:{tarPath}");

            templates.Workspace = new Template("WORKSPACE", workspaceContents);
            var workspaceMaker = new WorkspaceMaker(workspaceRoot, workspaceName, templates);
            workspaceMaker.Init();
            return 0;
        }

        public static int Init(InitOptions options)
        {
            var workspaceRoot = Directory.GetCurrentDirectory();
            var workspaceName = options.WorkspaceName;
            if (string.IsNullOrEmpty(workspaceName))
            {
                workspaceName = Path.GetFileName(workspaceRoot);
                ;
            }
            else
            {
                workspaceRoot = Path.Combine(workspaceRoot, workspaceName);
                Directory.CreateDirectory(workspaceRoot);
                Directory.SetCurrentDirectory(workspaceRoot);
            }

            var runfiles = Runfiles.Create();
            var templates = Templates.CreateDefault(runfiles);
            var workspaceMaker = new WorkspaceMaker(workspaceRoot, workspaceName, templates);
            workspaceMaker.Init();

            Console.WriteLine("Workspace created, next steps:");
            Console.WriteLine("bazel run //:gazelle");
            Console.WriteLine("bazel build //...");
            Console.WriteLine("bazel test //...");
            return 0;
        }

        public static int Gazelle(GazelleOptions options)
        {
            var runfiles = Runfiles.Create();
            var gazelle = FindGazelle(runfiles);

            if (gazelle == null) return 1;

            var process = Process.Start(gazelle);
            process!.WaitForExit();
            return process.ExitCode;
        }

        private static int Restore(RestoreOptions restore)
        {
            var workspace = BazelEnvironment.TryGetWorkspaceRoot();
            var cwd = Directory.GetCurrentDirectory();
            if (workspace != null && !cwd.StartsWith(workspace))
                Directory.SetCurrentDirectory(workspace);

            var query = $"kind(msbuild_restore,{restore.Package})";
            Console.WriteLine($"bazel query {query} | xargs bazel build");
            var info = new ProcessStartInfo("bazel") { RedirectStandardOutput = true };
            info.ArgumentList.Add("query");
            info.ArgumentList.Add(query);

            var queryProcess = Process.Start(info)!;
            var output = queryProcess.StandardOutput.ReadToEnd();
            queryProcess.WaitForExit();
            Console.WriteLine(output);
            if (queryProcess.ExitCode != 0) return queryProcess.ExitCode;
            var targets = output.Split(Environment.NewLine);
            var build = Process.Start("bazel", "build " + string.Join(' ', targets))!;

            build.WaitForExit();

            return build.ExitCode;
        }

        private static string? FindGazelle(Runfiles runfiles)
        {
            // are we released?
            var artifactsFolder = runfiles.Rlocation($"rules_msbuild/.azpipelines/artifacts");
            string? gazellePath = null;

            // if we're in nuget, runfiles will be directory-based, and this will work just fine on windows
            if (Directory.Exists(artifactsFolder))
            {
                var subPath = Util.GetGazellePath();
                gazellePath = Path.Combine(artifactsFolder, subPath);
            }
            else
            {
                string suffix = "";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    suffix = ".exe";
                gazellePath =
                    runfiles.Rlocation($"rules_msbuild/gazelle/dotnet/gazelle-dotnet_/gazelle-dotnet{suffix}");
            }

            if (gazellePath == null || !File.Exists(gazellePath))
            {
                Console.Error.WriteLine(
                    $"Failed to locate gazelle-dotnet binary at path {gazellePath}, please file an issue at github.com/samhowes/rules_msbuild");
                return null;
            }

            return gazellePath;
        }
    }
}