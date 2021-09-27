using System;
using System.IO;
using RulesMSBuild.Tools.Bazel;

namespace runfiles
{
    class Program
    {
        static void Main(string[] args)
        {
            var runfiles = Runfiles.Create();
            var path = runfiles.Rlocation("hello/RunfilesTest/foo.txt");
            Console.WriteLine(File.ReadAllText(path));
        }
    }
}
