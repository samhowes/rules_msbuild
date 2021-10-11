﻿using System;
using System.IO;
using RulesMSBuild.Tools.Bazel;

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

            var runfiles = Runfiles.Create<Program>();
            var templates = Templates.CreateDefault(runfiles.Runfiles);
            var workspaceMaker = new WorkspaceMaker(workspaceRoot, workspaceName, templates);
            workspaceMaker.Init();
            
            Console.WriteLine("Workspace created, next steps:");
            Console.WriteLine("bazel run //:gazelle");
            Console.WriteLine("bazel build //...");
            Console.WriteLine("bazel test //...");
        }
    }
}