using System;
using System.IO;

namespace MyRulesDotnet.Tools.RunfilesTests
{
    public static class DotnetCat
    {
        static void Main(string[] args)
        {
            var r = Runfiles.Create();
            var contents = File.ReadAllText(r.Rlocation("my_rules_dotnet/tests/dotnet/tools/runfiles/netcoreapp3.1/foo.txt"));

            Console.WriteLine(contents);
        }
    }
}
