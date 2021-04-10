using System;
using System.Linq;

namespace MyRulesDotnet.Tools.Builder
{
    public class Program
    {
        public static bool DebugEnabled = false;

        public static void Fail(string message)
        {
            Console.Error.WriteLine("[Builder] " + message);
            Environment.Exit(1);
        }

        public static void Debug(string message)
        {
            if (!DebugEnabled) return;
            Console.WriteLine("[Debug] " + message);
        }

        static void Main(string[] args)
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BUILDER_DEBUG")))
            {
                DebugEnabled = true;
                Debug($"Received {args.Length} arguments: {string.Join(" ", args)}");
            }

            var command = args[0];
            var commandArgsEnd = 1;
            for (; commandArgsEnd < args.Length; commandArgsEnd++)
                if (args[commandArgsEnd] == "--")
                    break;

            var commandArgs = args[1..commandArgsEnd];

            var passthroughArgs = commandArgsEnd == args.Length ? Array.Empty<string>() : args[(commandArgsEnd + 1)..];

            switch (command)
            {
                case "launcher":
                    MakeLauncher(commandArgs);
                    return;
                case "restore":
                    PostProcess(commandArgs, passthroughArgs);
                    return;
                case "build":
                    PreProcess(commandArgs, passthroughArgs);
                    return;
                default:
                    Fail($"Unknown command: {command}");
                    return;
            }
        }

        private static void PreProcess(string[] commandArgs, string[] passthroughArgs)
        {
            var processor = new OutputProcessor(new ProcessorContext(commandArgs, passthroughArgs));
            processor.PreProcess();
        }

        private static void PostProcess(string[] commandArgs, string[] passthroughArgs)
        {
            var processor = new OutputProcessor(new ProcessorContext(commandArgs, passthroughArgs));
            processor.PostProcess();
        }

        private static void MakeLauncher(string[] args)
        {
            var factory = new LauncherFactory();
            factory.Create(args);
        }
    }
}
