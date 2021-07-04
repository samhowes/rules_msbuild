using System;
using CommandLine;
using CommandLine.Text;

namespace Hello
{
    class EchoVerb
    {
        [Value(index: 0,
            Required = true,
            HelpText = "Echo text.")]
        public string EchoText { get; set; }
    }

    class Program
    {
        public static void Main(string[] args)
        {
            var parsed = Parser.Default.ParseArguments<EchoVerb>(new string[]{"<3"});
            var result = parsed.MapResult<EchoVerb, int>(
                (EchoVerb verb) => {
                    Console.WriteLine($"NuGet {verb.EchoText} Bazel");
                    return 0;
                },
                errs =>
                {
                    HelpText.AutoBuild(parsed);
                    return -1;
                }
            );
            Environment.Exit(result);
        }
    }
}
