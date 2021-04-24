using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

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
        private static Regex MsBuildVariableRegex = new Regex(@"\$\((\w+)\)", RegexOptions.Compiled);
        public static bool DebugEnabled = Environment.GetEnvironmentVariable("DOTNET_BUILDER_DEBUG") != null;

        public static int Fail(string message)
        {
            Console.Error.WriteLine("[Builder] " + message);
            Environment.Exit(1);
            return 1; // weird. Oh well.
        }

        public static void Debug(string message)
        {
            if (!DebugEnabled) return;
            Console.WriteLine("[Debug] " + message);
        }

        static int Main(string[] args)
        {
            if (DebugEnabled)
            {
                Debug($"Received {args.Length} arguments: {string.Join(" ", args)}");
            }

            var command = ParseArgs(args);

            var startupDirectory = Environment.CurrentDirectory;
            for (var i = 0; i < command.PassThroughArgs.Length; i++)
            {
                var arg = command.PassThroughArgs[i];
                
                command.PassThroughArgs[i] = MsBuildVariableRegex.Replace(arg, (match) =>
                {
                    if (match.Groups[1].Value == "MSBuildStartupDirectory")
                    {
                        return startupDirectory;
                    }

                    return match.Value;
                });

            }
            
            switch (command.Action)
            {
                case "launcher":
                    return MakeLauncher(command);
                    
                case "restore":
                    return PostProcess(command);
                    
                case "publish":
                case "build":
                    return PreProcess(command);
                    
                default:
                    return Fail($"Unknown command: {command.Action}");
                    
            }
        }

        private static Command ParseArgs(string[] args)
        {
            var command = new Command {Action = args[0]};
            ParseArgsImpl(args, 1, command);

            return command;
        }

        private static void ParseArgsImpl(string[] args, int start, Command command)
        {
            for (var i = start; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg == "@file")
                {
                    var fileArgs = File.ReadAllLines(args[i + 1])
                        .SelectMany(l => l.Split(' '))
                        .ToArray();
                    ParseArgsImpl(fileArgs, 0, command);
                    i++;
                    continue;
                }
                
                if (arg.Length == 0 || arg[0] != '-')
                {
                    command.PositionalArgs.Add(arg);
                    continue;
                }

                if (arg == "--")
                {
                    command.PassThroughArgs = args[(i + 1)..];
                    break;
                }

                // assume a well formed array of args in the form [`--name` `value`]
                var name = arg[2..];
                var value = args[i + 1];
                command.NamedArgs[name] = value;
                i++;
            }
        }

        private static int PreProcess(Command command)
        {
            var processor = new OutputProcessor(new ProcessorContext(command));
            return processor.PreProcess();
        }

        private static int PostProcess(Command command)
        {
            var processor = new OutputProcessor(new ProcessorContext(command));
            return processor.PostProcess();
        }

        private static int MakeLauncher(Command command)
        {
            var factory = new LauncherFactory();
            return factory.Create(command.PositionalArgs.ToArray());
        }
    }
}