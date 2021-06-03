using System;
using System.IO;
using MyRulesDotnet.Tools.Bazel;

namespace Bzl
{
    class Program
    {
        static void Main(string[] args)
        {
            var cwd = Directory.GetCurrentDirectory();
            var workspaceRoot = cwd;
            string workspaceName = Path.GetFileName(workspaceRoot);
            if (args.Length == 1)
            {
                workspaceName = args[0];
                workspaceRoot = Path.Combine(cwd, workspaceName);
            }

            var runfiles = Runfiles.Create<Program>("@my_rules_dotnet//dotnet/tools/Bzl");

            var workspaceMaker = new WorkspaceMaker(runfiles.Runfiles, workspaceRoot, workspaceName);
            workspaceMaker.Init();
            
            Console.WriteLine("Workspace created, next steps:");
            Console.WriteLine("bazel run //:gazelle");
            Console.WriteLine("bazel build //...");
            Console.WriteLine("bazel test //...");
        }
    }
}