using System;
using System.IO;
using RulesMSBuild.Tools.Bazel;

namespace RulesMSBuild.Tools.RunfilesTests
{
    public static class DotnetCat
    {
        static void Main(string[] args)
        {
            var r = Runfiles.Create();
            var contents = File.ReadAllText(r.Rlocation("rules_msbuild/tests/dotnet/tools/runfiles/integration/foo.txt"));

            Console.Write(contents);
        }
    }
}
