using System.Collections.Generic;
using RulesMSBuild.Tools.Bazel;

namespace BuilderTests
{
    public class Program
    {
        public static int Main(string[] args)
        {
            RulesMSBuild.Tools.Builder.Program.RegisterSdk("/usr/local/share/dotnet/sdk/5.0.203");

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