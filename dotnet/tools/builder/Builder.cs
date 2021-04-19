using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MyRulesDotnet.Tools.Builder
{
    public class Command
    {
        public string Action;
        public List<string> PositionalArgs = new List<string>();
        public Dictionary<string, string> NamedArgs = new Dictionary<string, string>();
        public string[] PassThroughArgs { get; set; }
    }

    public class Program
    {
        public static bool DebugEnabled = Environment.GetEnvironmentVariable("DOTNET_BUILDER_DEBUG") != null;

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

            var command = new Command {Action = args[0]};
            var thisArgsEnd = 1;
            for (; thisArgsEnd < args.Length; thisArgsEnd++)
            {
                var arg = args[thisArgsEnd];
                if (arg[0] != '-')
                {
                    command.PositionalArgs.Add(arg);
                    continue;
                }
                
                if (arg == "--")
                {
                    command.PassThroughArgs = args[(thisArgsEnd + 1)..];
                    break;
                }

                // assume a well formed array of args in the form [`--name` `value`]
                var name = arg[2..];
                var value = args[thisArgsEnd + 1];
                command.NamedArgs[name] = value;
                thisArgsEnd++;
            }

            switch (command.Action)
            {
                case "launcher":
                    MakeLauncher(command);
                    return;
                case "restore":
                    PostProcess(command);
                    return;
                case "build":
                    PreProcess(command);
                    return;
                default:
                    Fail($"Unknown command: {command}");
                    return;
            }
        }

        private static void PreProcess(Command command)
        {
            var processor = new OutputProcessor(new ProcessorContext(command));
            processor.PreProcess();
        }

        private static void PostProcess(Command command)
        {
            var processor = new OutputProcessor(new ProcessorContext(command));
            processor.PostProcess();
        }

        private static void MakeLauncher(Command command)
        {
            var factory = new LauncherFactory();
            factory.Create(command.PositionalArgs.ToArray());
        }
    }
}