using System;
using System.IO;
using MyRulesDotnet.Tools.Bazel;

namespace MyRulesDotnet.Tools.RunfilesTests
{
    public static class DotnetCat
    {
        static void Main(string[] args)
        {
            var r = Runfiles.Create();
            var contents = File.ReadAllText(r.Rlocation("my_rules_dotnet/tests/dotnet/tools/Runfiles/integration/foo.txt"));

            Console.Write(contents);
        }
    }
}
