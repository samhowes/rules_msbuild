using System;
using RulesMSBuild.Tools.Bazel;

namespace docs
{
    class Program
    {
        static void Main(string[] args)
        {
            var runfiles = Runfiles.Create<Program>();

            var workspaceRoot = BazelEnvironment.GetWorkspaceRoot();
            
            foreach (var arg in args)
            {
                Console.WriteLine(arg);
                Console.WriteLine(runfiles.Rlocation(arg));
            }
        }
    }
}
