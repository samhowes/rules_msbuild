#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace NuGetParser
{
    public class Restorer
    {
        private readonly NuGetContext _context;
        private readonly string _specPath;
        private readonly string _dotnetPath;
        private readonly string _testLogger;
        private readonly string _dir;

        public Restorer(NuGetContext context)
        {
            _context = context;
            _specPath = context.Args["spec_path"];
            _dotnetPath = context.Args["dotnet_path"];
            _testLogger = context.Args["test_logger"];
            
            _dir = Path.GetDirectoryName(_specPath)!;
        }

        public void Restore()
        {
            var (fetchProject, frameworks) = MakeRestoreProjects();
            var dotnet = Process.Start(
                new ProcessStartInfo(_dotnetPath,
                    $"restore {fetchProject}")
                {
                    WorkingDirectory = _dir
                });
            dotnet!.WaitForExit();
            _context.Frameworks = frameworks.Values.ToList();
        }

        private (string fetchPath, Dictionary<string, FrameworkInfo> frameworks) MakeRestoreProjects()
        {
            var specList = File.ReadAllLines(_specPath);
            var frameworks = ParsePackageGroups(specList);
            var dir = Path.GetDirectoryName(_specPath)!;
            var projects = new List<string>();
            foreach (var framework in frameworks.Values)
            {
                for (int i = 0; i < framework.RestoreGroups.Count; i++)
                {
                    var group = framework.RestoreGroups[i];
                    var builder = new ProjectBuilder("Microsoft.NET.Sdk");
                    builder.SetTfm(framework.Tfm);
                    builder.AddPackages(group.Packages.Select(p => (p.Key, p.Value)));

                    var projectFileName = $"{framework.Tfm}.{i + 1}.proj";
                    projects.Add(projectFileName);
                    var path = Path.Combine(dir!, projectFileName);

                    group.ProjectFileName = projectFileName;
                    group.ObjDirectory = Path.Combine(dir, "_obj", Path.GetFileNameWithoutExtension(projectFileName));
                    builder.Save(path);
                }
            }

            var fetchBuilder = new ProjectBuilder("Microsoft.Build.Traversal/3.0.3");
            fetchBuilder.AddReferences(projects);
            var fetchPath = Path.Combine(dir!, "nuget.fetch.proj");
            fetchBuilder.Save(fetchPath);
            return (fetchPath, frameworks);
        }

        private Dictionary<string, FrameworkInfo> ParsePackageGroups(string[] specList)
        {
            var frameworks = new Dictionary<string, FrameworkInfo>(StringComparer.OrdinalIgnoreCase);
            var loggerParts = _testLogger.Split("/");
            var loggerName = loggerParts[0];
            var loggerVersion = loggerParts[1];
            foreach (var spec in specList)
            {
                var parts = spec.Split(":");
                string? packageName = null;
                string packageVersion = null!;
                if (parts.Length > 1)
                {
                    var packageParts = parts[0].Split("/");
                    packageName = packageParts[0];
                    packageVersion = packageParts[1];
                }

                var tfms = parts.Last().Split(",");
                foreach (var tfm in tfms)
                {
                    if (string.IsNullOrEmpty(tfm))
                        throw new CustomException($"Invalid tfm '{tfm}'");

                    if (!frameworks.TryGetValue(tfm, out var tfmInfo))
                    {
                        tfmInfo = new FrameworkInfo(tfm);
                        frameworks[tfm] = tfmInfo;
                        tfmInfo.AddPackage(loggerName, loggerVersion);
                    }

                    if (packageName == null) continue;

                    tfmInfo.AddPackage(packageName, packageVersion);
                }
            }

            return frameworks;
        }
    }
}