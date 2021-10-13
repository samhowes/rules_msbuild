using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RulesMSBuild.Tools.Bazel;
using SamHowes.Bzl;

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

        static async Task<int> Main(string[] args)
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

            await DownloadArtifacts();

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

            var (tarAlias, usage) = BuildTar(work, version.Value);

            const string releaseNotes = "ReleaseNotes.md";
            var originalNotes = await File.ReadAllTextAsync(releaseNotes);
            const string marker = "<!--marker-->";
            var markerIndex = originalNotes.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0) Die("Failed to find marker in release notes");

            var notes = new StringBuilder(originalNotes[..(markerIndex + marker.Length)]);

            notes.AppendLine();
            notes.AppendLine("```python");
            notes.Append(usage);
            notes.AppendLine("```");

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

            await File.WriteAllTextAsync(releaseNotes, notes.ToString());

            Info("Building bzl...");
            var outputs = Bazel("build //dotnet/tools/Bzl:SamHowes.Bzl_nuget --//config:mode=release");
            var nupkg = outputs.Single();

            Info("Creating release...");
            if (false)
            {
                Run($"gh release create {version} ",
                    "--prerelease",
                    "--draft",
                    $"--title v{version}",
                    "-F ReleaseNotes.md",
                    tarAlias,
                    nupkg);

                Run($"dotnet nuget push {nupkg} --api-key {nugetApiKey} --source https://api.nuget.org/v3/index.json");
            }

            return 0;
        }

        private static async Task DownloadArtifacts()
        {
            var artifactsBase = ".azpipelines/artifacts";
            var inSourceControl = Run("git", "ls-files", artifactsBase).Split("\n").ToHashSet();
            var http = new HttpClient()
            {
                BaseAddress = new Uri("https://dev.azure.com/samhowes/rules_msbuild/_apis/build/builds")
            };
            var client = new JsonClient(http);

            var builds =
                await client.GetAsync<Response<List<Build>>>(
                    "?definitions=6&resultFilter=succeeded&branchName=refs/heads/master&$top=1");
            var buildLink = builds.Value[0].Links["self"].Href;
            var artifacts = await client.GetAsync<Response<List<JsonApiEntity<Artifact>>>>($"{buildLink}/artifacts");
            var downloaded = new List<string>();
            foreach (var artifact in artifacts.Value)
            {
                if (!artifact.Name.EndsWith("amd64")) continue;

                var url = artifact.Resource.DownloadUrl;
                var response = await http.GetAsync(url);
                if (!response.IsSuccessStatusCode) throw new Exception($"Failed to download {url}");
                var zipStream = await response.Content.ReadAsStreamAsync();
                var zipLib = new ZipArchive(zipStream);

                foreach (var entry in zipLib.Entries)
                {
                    var destPath = Path.Combine(artifactsBase, entry.FullName);
                    if (entry.FullName.EndsWith('/'))
                    {
                        Directory.CreateDirectory(destPath);
                        continue;
                    }

                    if (inSourceControl.Contains(destPath))
                        downloaded.Add(destPath);
                    await using var dest = File.Create(destPath);
                    await using var source = entry.Open();
                    await source.CopyToAsync(dest);
                }
            }

            Run("git", downloaded.Prepend("update-index --assume-unchanged").ToArray());
        }

        private static (string tarAlias, string usage) BuildTar(string work, string version)
        {
            foreach (var file in Directory.GetFiles("bazel-bin", "rules_msbuild.*"))
            {
                Console.WriteLine($"Removing old artifact: {file}");
                File.Delete(file);
            }
        
            var outputs = Bazel("build //:tar");
            var tarSource = outputs[0];
            var tarAlias = Path.Combine(work, $"rules_msbuild-{version}.tar.gz");
            Run($"ln -s {tarSource} {tarAlias}");

            var url =
                $"https://github.com/samhowes/rules_msbuild/releases/download/{version}/rules_msbuild-{version}.tar.gz";
            var workspaceTemplateContents =
                Util.UpdateWorkspaceTemplate(Runfiles.Create<Program>().Runfiles, tarSource, url);

            File.WriteAllText("dotnet/tools/Bzl/WORKSPACE.tpl", workspaceTemplateContents);

            var usage = workspaceTemplateContents;

            const string readmePath = "README.md";
            var readme = File.ReadAllText(readmePath);
            var regex = new Regex(@"(```python\s+#\s?\/\/WORKSPACE).*?(```)", RegexOptions.Singleline);
            
            File.WriteAllText(readmePath, regex.Replace(readme, "$1\n" + usage + "$2", 1));

            return (tarAlias, usage);
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

    public class JsonClient
    {
        private readonly HttpClient _http;

        public JsonClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<T> GetAsync<T>(string url)
        {
            var response = await _http.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new Exception($"HTTP {response.StatusCode} for {url}:\n{body}");
            return JsonConvert.DeserializeObject<T>(body);
        }
    }

    public class Response<T>
    {
        public int Count { get; set; }
        public T Value { get; set; }
    }

    public class DevOpsResource
    {
        [JsonProperty("_links")] public Dictionary<string, Link> Links { get; set; }
    }

    public class Link
    {
        public string Href { get; set; }
    }

    public class Build : DevOpsResource
    {
    }

    public class JsonApiEntity<T>
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public T Resource { get; set; }
    }

    public class Artifact
    {
        public string Url { get; set; }
        public string DownloadUrl { get; set; }
    }
}