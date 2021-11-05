using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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
        static Regex ReleaseRegex = new Regex(@"(?<name>.*)(\.release)(?<ext>\..*)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        static async Task<int> Main(string[] argsArray)
        {
            var args = argsArray.Select(a => a.TrimStart('-').Split('='))
                .ToDictionary(p => p[0], p => p[1]);

            var root = BazelEnvironment.TryGetWorkspaceRoot();
            if (root != null)
                Directory.SetCurrentDirectory(root);
            else
                root = Directory.GetCurrentDirectory();

            var process = Process.Start(new ProcessStartInfo("git", "ls-files") {RedirectStandardOutput = true});

            if (args.TryGetValue("tar", out var outputName))
            {
                outputName = outputName.Split(" ")[0];
            }
            else
                outputName = "test.tar.gz";

            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (;;)
            {
                var actual = await process!.StandardOutput.ReadLineAsync();
                if (actual == null) break;
                if (Path.GetFileName(actual) == outputName) continue;

                var tarValue = actual;
                actual = Path.Combine(root, actual);
                var match = ReleaseRegex.Match(actual);
                if (match.Success)
                {
                    tarValue = match.Groups["name"].Value + match.Groups["ext"].Value;
                    tarValue = Path.GetRelativePath(root, tarValue);
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
                    "rules_msbuild/dotnet/tools/launcher/launcher_windows_/launcher_windows.exe");
            if (File.Exists(debugLauncher))
            {
                var launcherPath = runfiles.Rlocation(debugLauncher);
                files[".azpipelines/artifacts/windows-amd64/launcher_windows.exe"] = launcherPath;
            }

            if (args.TryGetValue("packages", out var packagesString))
            {
                var packages = packagesString.Split(",");
                foreach (var package in packages)
                {
                    files[$".azpipelines/artifacts/packages/{Path.GetFileName(package)}"] = Path.GetFullPath(package);
                }
            }

            await using (var output = File.Create(outputName))
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
            await using (var outputRead = File.OpenRead(outputName))
            using (var sha = SHA256.Create())
            {
                outputRead.Position = 0;
                var hash = await sha.ComputeHashAsync(outputRead);
                hashValue = Convert.ToHexString(hash).ToLower();
            }

            Console.WriteLine($"SHA256 = {hashValue}");
            await File.WriteAllTextAsync(outputName + ".sha256", hashValue);

            return 0;
        }
    }
}