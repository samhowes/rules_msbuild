using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace release
{
    public static class Util
    {
        public static void Info(string message) => Console.WriteLine(message);

        public static void Die(string message)
        {
            Console.WriteLine(message);
            Environment.Exit(1);
        }

        public static string Run(string command, params string[] args)
        {
            var (process, output) = RunImpl(string.Join(" ", args.Prepend(command)));
            if (process.ExitCode != 0) Die("Command failed");
            return output;
        }

        public static string TryRun(string command)
        {
            var (process, output) = RunImpl(command);
            if (process.ExitCode != 0) return null;
            return output;
        }

        public static (Process process, string) RunImpl(string command)
        {
            var parts = command.Split(' ');
            var filename = parts[0];
            var args = string.Join(' ', parts.Skip(1));
            var process = Process.Start(new ProcessStartInfo(filename, args)
            {
                RedirectStandardOutput = true
            });
            var builder = new StringBuilder();
            process!.OutputDataReceived += (_, data) =>
            {
                builder.AppendLine(data.Data);
                Console.Out.WriteLine(data.Data);
                Console.Out.Flush();
            };
            process!.BeginOutputReadLine();
            process!.WaitForExit();
            return (process, builder.ToString());
        }


        public static List<string> Bazel(string args)
        {
            var startInfo = new ProcessStartInfo("bazel", args)
            {
                RedirectStandardError = true
            };
            var process = Process.Start(startInfo);
            var outputs = new List<string>();
            var readOutputs = false;
            process!.ErrorDataReceived += (_, data) =>
            {
                Console.Error.WriteLine($"{data.Data}");
                var line = data.Data;
                if (string.IsNullOrEmpty(line)) return;

                if (readOutputs)
                {
                    if (line[0..2] != "  ")
                    {
                        readOutputs = false;
                        return;
                    }

                    outputs.Add(Path.GetFullPath(line[2..]));
                }

                if (line.IndexOf("up-to-date:", StringComparison.Ordinal) > 0)
                {
                    readOutputs = true;
                }
            };

            process.Start();
            process.BeginErrorReadLine();
            process.WaitForExit();
            if (process.ExitCode != 0) Die("Command failed");
            return outputs;
        }
        
        public static T RunJson<T>(string command)
        {
            var type = typeof(T);
            if (type.IsGenericType)
                type = type.GetGenericArguments()[0];
            var fields = type.GetProperties().Select(p => p.Name[0].ToString().ToLower() + p.Name[1..]);
            var result = Run(command + " " + string.Join(",", fields));
            return JsonConvert.DeserializeObject<T>(result);
        }

        public static void Copy(string src, string dest)
        {
            var templateDest = new FileInfo(dest);
            File.Copy(src, templateDest.FullName, true);
            templateDest.IsReadOnly = false;
        }
    }
}