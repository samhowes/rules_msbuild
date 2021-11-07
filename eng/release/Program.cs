using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RulesMSBuild.Tools.Bazel;
using static release.Util;

namespace release
{
    enum Action
    {
        Release,
        Clean,
        Test,
    }

    class Program
    {
        private static Action _action;
        private static string _work;
        private static string _root;
        private static string _nuGetApiKey;
        private static string _version;

        static async Task<int> Main(string[] args)
        {
            Setup(args);

            await DownloadArtifacts();

            if (_action == Action.Clean)
            {
                Run($"gh release delete \"{_version}\" -y");
                Run($"git push --delete origin \"{_version}\"");
                return 0;
            }

            VerifyNewRelease();

            var (tarAlias, usage) = BuildTar(_work, _version);

            await MakeNotes(usage);

            Info("Building bzl...");
            var outputs = Bazel("build //dotnet/tools/Bzl:Bzl.nupkg --//config:mode=release");
            var nupkg = outputs.Single();

            File.WriteAllText(Path.Combine(_root, "commitmessage.txt"), $"release version {_version}");

            if (_action == Action.Release)
            {
                Info("Creating release...");
                Run($"gh release create {_version} ",
                    "--prerelease",
                    "--draft",
                    $"--title v{_version}",
                    "-F ReleaseNotes.md",
                    tarAlias,
                    nupkg);

                Run($"dotnet nuget push {nupkg} --api-key {_nuGetApiKey} --source https://api.nuget.org/v3/index.json");
                UpdateVersion();
            }
            else if (_action == Action.Test)
            {
                VerifyTar();
            }

            return 0;
        }

        private static void UpdateVersion()
        {
            var versionParts = _version.Split(".").Select(int.Parse).ToArray();
            versionParts[^1]++;
            var newString = string.Join(".", versionParts);
            File.WriteAllText(Path.Combine(_root, "version.bzl"), $"VERSION = \"{newString}\"");
        }

        private static void VerifyTar()
        {
            var test = Path.Combine(_work, "test");
            if (Directory.Exists(test)) Directory.Delete(test, true);
            Directory.CreateDirectory(test);

            var nupkg = Path.GetFullPath($"bazel-bin/dotnet/tools/Bzl/SamHowes.Bzl.{_version}.nupkg");
            var tar = Path.Combine(_work, "rules_msbuild.tar.gz");
            File.Copy("bazel-bin/rules_msbuild.tar.gz", tar);
            File.Copy("bazel-bin/rules_msbuild.tar.gz.sha256", Path.Combine(_work, "rules_msbuild.tar.gz.sha256"));

            Directory.SetCurrentDirectory(_work);
            Run($"unzip {nupkg} -d nupkg");
            Run($"chmod -R 755 nupkg");

            Directory.SetCurrentDirectory(test);
            var tool = Path.Combine(_work, "nupkg/tools/netcoreapp3.1/any/Bzl.dll");

            Run("dotnet new console -o console --no-restore");
            Run($"dotnet exec {tool} _test {tar}");
            Bazel("clean --expunge");
            Bazel("run //:gazelle");
            var result = TryRun("bazel run //console");
            if (result.Trim() != "Hello World!")
            {
                Die($"test failed, bad output: {result}");
            }

            Info("SUCCESS");
        }

        private static async Task MakeNotes(string usage)
        {
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

            notes.AppendLine($"[SamHowes.Bzl: {_version}](https://www.nuget.org/packages/SamHowes.Bzl/{_version})")
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
        }

        private static void Setup(string[] args)
        {
            _action = Action.Release;
            if (args.Length > 0)
            {
                if (!Enum.TryParse<Action>(args[0], true, out _action))
                    Die($"Failed to parse action from {args[0]}");
            }

            _nuGetApiKey = Environment.GetEnvironmentVariable("NUGET_API_KEY");
            if (_action == Action.Release && string.IsNullOrEmpty(_nuGetApiKey))
            {
                Die("No NUGET_API_KEY found");
            }

            _work = Path.Combine(Directory.GetCurrentDirectory(), "_work");
            if (Directory.Exists(_work))
                Directory.Delete(_work, true);
            Directory.CreateDirectory(_work);

            Info($"Work directory: {_work}");
            _root = BazelEnvironment.GetWorkspaceRoot();
            Directory.SetCurrentDirectory(_root);

            var versionContents = File.ReadAllText(Path.Combine(_root, "version.bzl"));
            var versionMatch = Regex.Match(versionContents, @"VERSION.*?=.*?""([^""]+)""");
            if (!versionMatch.Success) Die("Failed to parse version from version.bzl");
            _version = versionMatch.Groups[1].Value;
            Info($"Using version: {_version}");
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

            var outputs = Bazel("build //:tar --//config:mode=release");
            var tarSource = outputs[0];
            var tarAlias = Path.Combine(work, $"rules_msbuild-{version}.tar.gz");
            Run($"ln -s {tarSource} {tarAlias}");

            var url =
                $"https://github.com/samhowes/rules_msbuild/releases/download/{version}/rules_msbuild-{version}.tar.gz";
            var workspaceTemplateContents =
                Bzl.Util.UpdateWorkspaceTemplate(Runfiles.Create(), tarSource, url);

            File.WriteAllText("dotnet/tools/Bzl/WORKSPACE.tpl", workspaceTemplateContents);

            var usage = workspaceTemplateContents;

            const string readmePath = "README.md";
            var readme = File.ReadAllText(readmePath);
            var regex = new Regex(@"(```python\s+#\s?\/\/WORKSPACE).*?(```)", RegexOptions.Singleline);

            File.WriteAllText(readmePath, regex.Replace(readme, "$1\n" + usage + "$2", 1));

            return (tarAlias, usage);
        }

        private static void VerifyNewRelease()
        {
            Info("Checking for existing release...");
            var existingRelease = TryRun($"gh release view {_version}");
            if (existingRelease != null)
            {
                Die($"Failed to release: {_version} already exists");
            }
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