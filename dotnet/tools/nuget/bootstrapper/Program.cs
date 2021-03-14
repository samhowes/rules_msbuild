using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace bootstrapper
{
    internal class Program
    {
        private static BazelEnvironment Env = new BazelEnvironment();

        private static void Main(string[] args)
        {
            Env.GetRunFiles();

            var rootLocation = File.ReadAllLines(Env.Runfiles!.ShortDict["ROOT_LOCATION"].AbsolutePath).First();
            var rootDir = Path.GetDirectoryName(rootLocation);
            var repoRoot = Path.Combine(Env.WorkspaceExecRoot, rootDir);
            var buildPath = Path.Combine(repoRoot, "BUILD");

            var buildContents = File.ReadAllText(buildPath);

            //const string repositoryName = "nuget";
            TextReader reader;
            if (args.Length > 0)
            {
                var xmlPath = args[0];
                // if we are running under Bazel run, assume relative to the workspace directory
                var xmlLabel = new Label(xmlPath);
                Env.ResolveLabel(xmlLabel);

                if (!File.Exists(xmlLabel.Filepath))
                {
                    Console.WriteLine($"Invalid path: {xmlPath}");
                    Environment.Exit(1);
                }

                reader = new StreamReader(xmlLabel.Filepath);
            }
            else
            {
                reader = Console.In;
            }

            var queryParser = new QueryParser(reader);
            var targets = queryParser.GetTargets(Env.WorkspaceName);
            using var writer = new StreamWriter(buildPath);
            var buildWriter = new BuildWriter(writer);
            buildWriter.Write(buildContents, targets);

            writer.Close();
        }
    }

    public class BuildWriter
    {
        private static readonly Regex Regex = new Regex(@"^(?<indent>\s+)(?<comment>#\s?bootstrap:(?<variable_name>\w+)\s*)",
                RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        private readonly StreamWriter _writer;

        public BuildWriter(StreamWriter writer)
        {
            _writer = writer;
        }

        public void Write(string buildContents, List<DotnetTarget> targets)
        {
            var tfms = targets.SelectMany(t => t.Tfms);
            var writeIndex = 0;

            void WriteSpan(int length)
            {
                var span = ((ReadOnlySpan<char>)buildContents).Slice(writeIndex, length);
                _writer.Write(span);
            }

            foreach (Match match in Regex.Matches(buildContents))
            {
                var indent = match.Groups["indent"];
                void WriteListItem(string value)
                {
                    _writer.WriteLine($"{indent}\"{value}\",");
                }

                WriteSpan(match.Index - writeIndex);
                writeIndex = match.Index + match.Value.Length;

                switch (match.Groups["variable_name"].Value)
                {
                    case "tfms":
                        foreach (var tfm in tfms)
                            WriteListItem(tfm);
                        break;

                    case "deps":
                        foreach (var target in targets)
                            WriteListItem(target.Label.FullName);
                        break;
                }
                _writer.Write(match.Value);
            }
            WriteSpan(buildContents.Length - writeIndex);
            _writer.Flush();
        }
    }
}