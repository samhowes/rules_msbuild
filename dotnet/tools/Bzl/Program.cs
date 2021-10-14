#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using CommandLine;
using RulesMSBuild.Tools.Bazel;
using SamHowes.Bzl;

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

    class Program
    {
        static int Main(string[] args)
        {
            if (args[0] == "_test")
                return Test(args[1]);
            
            return Parser.Default.ParseArguments<InitOptions, GazelleOptions>(args)
                .MapResult(
                    (InitOptions init) => Init(init),
                    (GazelleOptions gazelle) => Gazelle(gazelle),
                    errors => 1);
        }

        private static int Test(string tarPath)
        {
            var workspaceRoot = Directory.GetCurrentDirectory();
            var workspaceName = Path.GetFileName(workspaceRoot);
            var runfiles = Runfiles.Create<Program>();
            var templates = Templates.CreateDefault(runfiles.Runfiles);

            var workspaceContents =
                Util.UpdateWorkspaceTemplate(runfiles.Runfiles, tarPath, $"file:{tarPath}");

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

            var runfiles = Runfiles.Create<Program>();
            var templates = Templates.CreateDefault(runfiles.Runfiles);
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
            var runfiles = Runfiles.Create<Program>();
            var gazelle = FindGazelle(runfiles);

            if (gazelle == null) return 1;

            var process = Process.Start(gazelle);
            process!.WaitForExit();
            return process.ExitCode;
        }

        private static string? FindGazelle(LabelRunfiles runfiles)
        {
            // are we released?
            var artifactsFolder = runfiles.Rlocation($"//.azpipelines/artifacts:gazelle-dotnet");
            string? gazellePath = null;

            // if we're in nuget, runfiles will be directory-based, and this will work just fine on windows
            if (Directory.Exists(artifactsFolder))
            {
                var subPath = Util.GetGazellePath();
                gazellePath = Path.Combine(artifactsFolder, subPath);
            }
            else
            {
                gazellePath = runfiles.Rlocation("//gazelle/dotnet:gazelle-dotnet_/gazelle-dotnet");
            }

            if (gazellePath == null || !File.Exists(gazellePath))
            {
                Console.Error.WriteLine(
                    "Failed to locate gazelle-dotnet binary, please file an issue at github.com/samhowes/rules_msbuild");
                return null;
            }

            return gazellePath;
        }
    }
}