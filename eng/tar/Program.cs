using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using RulesMSBuild.Tools.Bazel;
using Bzl;

namespace tar
{
    class Program
    {
        static async Task<int> Main(string[] argsArray)
        {
            var args = argsArray.Select(a => a.TrimStart('-').Split('='))
                .ToDictionary(p => p[0], p => p[1]);

            var root = BazelEnvironment.TryGetWorkspaceRoot();
            if (root != null)
                Directory.SetCurrentDirectory(root);
            else
                root = Directory.GetCurrentDirectory();

            if (args.TryGetValue("tar", out var outputName))
            {
                outputName = outputName.Split(" ")[0];
            }
            else
                outputName = "test.tar.gz";

            var packages = Array.Empty<string>();
            if (args.TryGetValue("packages", out var packagesString))
                packages = packagesString.Split(",");
            using var tarMaker = new TarMaker(outputName, root, packages);
            return await tarMaker.MakeTar();
        }
    }

    public class TarMaker : IDisposable
    {
        private static readonly Regex ReleaseRegex = new Regex(@"(?<name>.*)(\.release)(?<ext>\..*)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex PublicContentsRegex = new Regex(
            @"\n([^\n]+?)(rules_msbuild:release start)(?<public>.*?)((\n([^\n]+?)(rules_msbuild:release end))|$)",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private readonly string _outputName;
        private readonly string _root;
        private readonly string[] _packages;
        private List<string> _tempFiles = new();

        public TarMaker(string outputName, string root, string[] packages)
        {
            _outputName = outputName;
            this._root = root;
            _packages = packages;
        }

        public async Task<int> MakeTar()
        {
            var process = Process.Start(new ProcessStartInfo("git", "ls-files") {RedirectStandardOutput = true});

            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (;;)
            {
                var actual = await process!.StandardOutput.ReadLineAsync();
                if (actual == null) break;
                if (Path.GetFileName(actual) == _outputName) continue;

                var tarValue = actual;
                actual = Path.Combine(_root, actual);
                var match = ReleaseRegex.Match(actual);
                if (match.Success)
                {
                    tarValue = match.Groups["name"].Value + match.Groups["ext"].Value;
                    tarValue = Path.GetRelativePath(_root, tarValue).Replace('\\','/');
                }
                else
                {
                    switch (Path.GetExtension(actual))
                    {
                        case ".bazel":
                            actual = HidePrivateContent(actual);
                            break;
                    }
                }

                files[tarValue] = actual;
            }

            // wait for exit AFTER reading all the standard output:
            // https://stackoverflow.com/questions/439617/hanging-process-when-run-with-net-process-start-whats-wrong
            await process!.WaitForExitAsync();
            if (process.ExitCode != 0) return process.ExitCode;

            var runfiles = Runfiles.Create();
            var exe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
            var debugGazelle =
                runfiles.Rlocation($"rules_msbuild/gazelle/dotnet/gazelle-dotnet_/gazelle-dotnet{exe}");
            if (File.Exists(debugGazelle))
            {
                var gazellePath = Util.GetGazellePath();
                files[$".azpipelines/artifacts/{gazellePath}"] = debugGazelle;
            }

            var debugLauncher =
                runfiles.Rlocation(
                    "rules_msbuild/dotnet/tools/launcher/launcher_windows_go_/launcher_windows_go.exe");
            if (File.Exists(debugLauncher))
            {
                var launcherPath = runfiles.Rlocation(debugLauncher);
                files[".azpipelines/artifacts/windows-amd64/launcher_windows.exe"] = launcherPath;
            }

            foreach (var package in _packages)
            {
                files[$".azpipelines/artifacts/packages/{Path.GetFileName(package)}"] = Path.GetFullPath(package);
            }

            await using (var output = File.Create(_outputName))
            await using (var gzoStream = new GZipOutputStream(output))
            using (var tarArchive = TarArchive.CreateOutputTarArchive(gzoStream))
            {
                foreach (var tarEntry in files.Keys.OrderBy(k => k))
                {
                    var entry = TarEntry.CreateEntryFromFile(files[tarEntry]);
                    // https://github.com/dotnet/runtime/issues/24655#issuecomment-566791742
                    await using (var stream = File.OpenRead(files[tarEntry]))
                    {
                        entry.TarHeader.Size = stream.Length;
                    }

                    entry.Name = tarEntry;
                    Console.WriteLine(entry.Name);

                    tarArchive.WriteEntry(entry, false);
                }

                tarArchive.Close();
            }

            string hashValue;
            await using (var outputRead = File.OpenRead(_outputName))
            using (var sha = SHA256.Create())
            {
                outputRead.Position = 0;
                var hash = await sha.ComputeHashAsync(outputRead);
                hashValue = Convert.ToHexString(hash).ToLower();
            }

            Console.WriteLine($"SHA256 = {hashValue}");
            await File.WriteAllTextAsync(_outputName + ".sha256", hashValue);

            return 0;
        }

        private string HidePrivateContent(string actual)
        {
            var contents = File.ReadAllText(actual);
            var builder = new StringBuilder();
            foreach (var match in PublicContentsRegex.Matches(contents).Cast<Match>())
            {
                builder.Append(match.Groups["public"].Value);
            }

            if (builder.Length == 0) return actual;

            var tmp = Path.Combine(BazelEnvironment.GetTmpDir(), Guid.NewGuid().ToString());
            _tempFiles.Add(tmp);
            File.WriteAllText(tmp, builder.ToString());
            return tmp;
        }

        public void Dispose()
        {
            foreach (var file in _tempFiles)
            {
                File.Delete(file);
            }
        }
    }
}
