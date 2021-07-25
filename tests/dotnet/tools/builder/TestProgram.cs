using System;
using System.Collections.Generic;
using System.IO;
using RulesMSBuild.Tools.Bazel;

namespace BuilderTests
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var sdkRoot = Environment.GetEnvironmentVariable("BAZEL_DOTNET_SDKROOT");

            if (sdkRoot == null)
            {
                //"/usr/local/share/dotnet/sdk/5.0.203"
                throw new Exception("BAZEL_DOTNET_SDKROOT is not set, cannot test.");
            }
            sdkRoot = Path.GetFullPath(sdkRoot);
            RulesMSBuild.Tools.Builder.Program.RegisterSdk(sdkRoot);

            var newArgs = new List<string>(args)
            {
                "-nocolor"
            };
            newArgs.Insert(0, typeof(Program).Assembly.Location);

            var testXml = BazelEnvironment.GetTestXmlPath();
            if (!string.IsNullOrEmpty(testXml))
            {
                newArgs.Add("-junit");
                newArgs.Add(testXml);
            }
            
            int returnCode = Xunit.ConsoleClient.Program.Main(newArgs.ToArray());

            return returnCode;
        }
    }
}