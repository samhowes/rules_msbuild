using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using RulesMSBuild.Tools.Bazel;

namespace release
{
    class Program
    {
        private static void Info(string message) => Console.WriteLine(message);

        private static void Die(string message)
        {
            Console.WriteLine(message);
            Environment.Exit(1);
        }

        private static string Run(string command, params string[] args)
        {
            var (process, output) = RunImpl(string.Join(" ", args.Prepend(command)));
            if (process.ExitCode != 0) Die("Command failed");
            return output;
        }

        private static string TryRun(string command)
        {
            var (process, output) = RunImpl(command);
            if (process.ExitCode != 0) return null;
            return output;
        }

        private static (Process process, string) RunImpl(string command)
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


        private static List<string> Bazel(string args)
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

        enum Action
        {
            Release,
            Clean
        }

        static int Main(string[] args)
        {
            var nugetApiKey = Environment.GetEnvironmentVariable("NUGET_API_KEY");
            if (string.IsNullOrEmpty(nugetApiKey))
            {
                Die("No NUGET_API_KEY found");
            }

            var action = Action.Release;
            if (args.Length > 0)
            {
                if (!Enum.TryParse<Action>(args[0], true, out action))
                    Die($"Failed to parse action from {args[0]}");
            }

            // var env = Environment.GetEnvironmentVariables();
            // foreach (var key in env.Keys.Cast<string>().OrderBy(k => k))
            // {
            //     Info($"{key}={env[key]}");
            // }

            var work = Path.Combine(Directory.GetCurrentDirectory(), "_work");
            if (Directory.Exists(work))
                Directory.Delete(work, true);
            Directory.CreateDirectory(work);

            Info($"Work directory: {work}");
            var root = BazelEnvironment.GetWorkspaceRoot();
            Directory.SetCurrentDirectory(root);

            var versionContents = File.ReadAllText(Path.Combine(root, "version.bzl"));
            var versionMatch = Regex.Match(versionContents, @"VERSION.*?=.*?""([^""]+)""");
            if (!versionMatch.Success) Die("Failed to parse version from version.bzl");
            var version = versionMatch.Groups[1];
            Info($"Using version: {version}");

            if (action == Action.Clean)
            {
                Run($"gh release delete \"{version}\" -y");
                Run($"git push --delete origin \"{version}\"");
                return 0;
            }

            VerifyNewRelease(version);

            var tarAlias = BuildTar(work, version, out var releaseNotes);

            var originalNotes = File.ReadAllText(releaseNotes);
            const string marker = "<!--marker-->";
            var markerIndex = originalNotes.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0) Die("Failed to find marker in release notes");

            var notes = new StringBuilder(originalNotes[..(markerIndex + marker.Length + 2)]);

            notes.AppendLine($"[SamHowes.Bzl: {version}](https://www.nuget.org/packages/SamHowes.Bzl/{version})")
                .AppendLine();

            var lastRelease = RunJson<GitHubRelease>("gh release view --json");
            var prs = RunJson<List<GitHubPr>>(
                $"gh pr list --search \"is:closed closed:>={lastRelease.CreatedAt}\" --json");

            notes.AppendLine("Changelog:");
            for (var i = 0; i < prs.Count; i++)
            {
                var pr = prs[i];
                notes.AppendLine($"{i + 1}. [PR #{pr.Number}: {pr.Title}]({pr.Url})");
                var toMatch = pr.Title + pr.Body;
                var closed = string.Join(", ", Regex.Matches(toMatch, @"#\d+").Select(m => m.Value));
                notes.Append("  Issues: ");
                notes.AppendLine(closed);
            }

            File.WriteAllText(releaseNotes, notes.ToString());

            Info("Building bzl...");
            var outputs = Bazel("build //dotnet/tools/Bzl:SamHowes.Bzl_nuget");
            var nupkg = outputs.Single();

            Info("Creating release...");
            Run($"gh release create {version} ",
                "--prerelease",
                "--draft",
                $"--title v{version}",
                "-F ReleaseNotes.md",
                tarAlias,
                nupkg);

            Run($"dotnet nuget push {nupkg} --api-key {nugetApiKey} --source https://api.nuget.org/v3/index.json");

            return 0;
        }

        private static string BuildTar(string work, Group version, out string releaseNotes)
        {
            var outputs = Bazel("build //:tar");
            var tarSource = outputs[0];
            var tarAlias = Path.Combine(work, $"rules_msbuild-{version}.tar.gz");
            Run($"ln -s {tarSource} {tarAlias}");

            var workspaceTemplate = "dotnet/tools/Bzl/WORKSPACE.tpl";
            Copy(outputs[1], workspaceTemplate);
            releaseNotes = "ReleaseNotes.md";
            Copy(outputs[2], releaseNotes);

            var usage = string.Join("\n", File.ReadAllLines(workspaceTemplate).Skip(2));

            const string readmePath = "README.md";
            var readme = File.ReadAllText(readmePath);

            File.WriteAllText(readmePath,
                Regex.Replace(readme, @"(```python\s+#\s?\/\/WORKSPACE).*?(```)", "$1\n" + usage + "\n$2",
                    RegexOptions.Singleline));

            return tarAlias;
        }

        private static void VerifyNewRelease(Group version)
        {
            Info("Checking for existing release...");
            var existingRelease = TryRun($"gh release view {version}");
            if (existingRelease != null)
            {
                Die($"Failed to release: {version} already exists");
            }
        }

        private static T RunJson<T>(string command)
        {
            var type = typeof(T);
            if (type.IsGenericType)
                type = type.GetGenericArguments()[0];
            var fields = type.GetProperties().Select(p => p.Name[0].ToString().ToLower() + p.Name[1..]);
            var result = Run(command + " " + string.Join(",", fields));
            return JsonConvert.DeserializeObject<T>(result);
        }

        private static void Copy(string src, string dest)
        {
            var templateDest = new FileInfo(dest);
            File.Copy(src, templateDest.FullName, true);
            templateDest.IsReadOnly = false;
        }
    }

    public class GitHubPr
    {
        public string Title { get; set; }
        public string Body { get; set; }
        public string Url { get; set; }
        public string Number { get; set; }
    }

    public class GitHubRelease
    {
        public string CreatedAt { get; set; }
    }
}