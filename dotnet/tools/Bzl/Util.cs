#nullable enable
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using RulesMSBuild.Tools.Bazel;

namespace Bzl
{
    public static class Util
    {
        public static string UpdateWorkspaceTemplate(Runfiles runfiles, string tarPath, string url)
        {
            var workspaceTemplate =
                File.ReadAllText(runfiles.Rlocation("rules_msbuild/dotnet/tools/Bzl/WORKSPACE.tpl"));
            var sha256 = File.ReadAllText(tarPath + ".sha256");

            var indexMatch = Regex.Match(workspaceTemplate, @"http_archive\(.*\s+name = ""rules_msbuild"",",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var index = (indexMatch.Index + indexMatch.Length);
            var section = workspaceTemplate[index..];

            var replaced = false;
            section = Regex.Replace(section,
                @"((?<indent>\s+)(?<key>\w+) = (?<value>\[?""[^""]+\""\]?,))",
                match =>
                {
                    var key = match.Groups["key"];
                    string value;
                    switch (key.Value)
                    {
                        case "sha256":
                            value = $"sha256 = \"{sha256}\",";
                            break;
                        case "urls":
                            value = $"urls = [\"{url}\"],";
                            break;
                        default:
                            throw new Exception($"Unexpected match: {match.Value}");
                    }

                    replaced = true;
                    return match.Groups["indent"].Value + value;
                }, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!replaced)
                throw new Exception("Failed to replace url in workspace");
            workspaceTemplate = workspaceTemplate[..index] + section;
            return workspaceTemplate;
        }

        public static string GetGazellePath()
        {
            string releasedSubfolder;
            string gazelleName = "gazelle-dotnet";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            {
                releasedSubfolder = "linux";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                releasedSubfolder = "darwin";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                releasedSubfolder = "windows";
                gazelleName += ".exe";
            }
            else
            {
                Console.Error.WriteLine($"Unknown platform: {Environment.OSVersion}");
                return null!;
            }

            releasedSubfolder = $"{releasedSubfolder}-amd64";
            return string.Join('/', releasedSubfolder, gazelleName);
        }
    }
}
