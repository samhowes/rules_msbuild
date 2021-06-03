using System;
using MyRulesDotnet.Tools.Bazel;

namespace Bzl
{
    class Program
    {
        static void Main(string[] args)
        {
            var runfiles = Runfiles.Create<Program>("@my_rules_dotnet//dotnet/tools/Bzl");
        }
    }
}