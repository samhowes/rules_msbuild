using System;

namespace RulesMSBuild.Tools.NuGetParser
{
    public static class BazelLogger
    {
        public static int Fail(string message)
        {
            Console.Error.WriteLine("[Builder] " + message);
            Environment.Exit(1);
            return 1; // weird. Oh well.
        }

        public static bool DebugEnabled = Environment.GetEnvironmentVariable("DOTNET_BUILDER_DEBUG") != null;
        public static bool VerboseEnabled = Environment.GetEnvironmentVariable("DOTNET_BUILDER_DEBUG") == "v";

        public static void Verbose(string message)
        {
            if (!VerboseEnabled) return;
            Console.WriteLine("[Verbose] " + message);
        }
        public static void Debug(string message)
        {
            if (!DebugEnabled) return;
            Console.WriteLine("[Debug] " + message);
        }
    }
}