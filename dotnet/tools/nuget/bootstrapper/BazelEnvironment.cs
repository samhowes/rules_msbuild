#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace bootstrapper
{
    public class BazelEnvironment
    {
        public static string WorkingDirectory { get; } = Environment.GetEnvironmentVariable("BUILD_WORKING_DIRECTORY")!;
        public static string WorkspaceDirectory { get; } = Environment.GetEnvironmentVariable("BUILD_WORKSPACE_DIRECTORY")!;
        public string ManifestFileName { get; } = "MANIFEST";

        public BazelEnvironment()
        {
            StartingDirectory = Directory.GetCurrentDirectory();
            ManifestPath = Path.Combine(StartingDirectory, ManifestFileName);
            const string execroot = "execroot";
            var endIndex = StartingDirectory.IndexOf(execroot) + execroot.Length;
            ExecRoot = StartingDirectory.Substring(0, endIndex);
            OutputBase = Path.GetDirectoryName(ExecRoot)!;
            WorkspaceName = StartingDirectory.Substring(endIndex + 1).Split(Path.DirectorySeparatorChar, 2)[0];
            WorkspaceExecRoot = Path.Combine(ExecRoot, WorkspaceName);
        }

        public string StartingDirectory { get; }
        public string ManifestPath { get; }
        public string ExecRoot { get; }
        public string OutputBase { get; }
        public string WorkspaceName { get; }
        public string WorkspaceExecRoot { get; }
        public Runfiles? Runfiles { get; private set; }

        public override string ToString()
        {
            var staticProps = new[]
            {
                nameof(WorkingDirectory),
                nameof(WorkspaceDirectory),
            };

            var instanceProps = new[]
            {
                nameof(OutputBase),
                nameof(ExecRoot),
                nameof(WorkspaceName),
                nameof(WorkspaceExecRoot),
                nameof(StartingDirectory),
                nameof(ManifestPath)
            };

            var builder = new StringBuilder();
            var type = typeof(BazelEnvironment);
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance).ToDictionary(p => p.Name);
            void WriteProps(object? self, IEnumerable<string> propNames)
            {
                foreach (var propName in propNames)
                {
                    var prop = properties[propName];
                    builder.Append(prop.Name);
                    builder.Append(": ");
                    builder.AppendLine((string?)prop.GetValue(self));
                }
            }

            WriteProps(null, staticProps);
            WriteProps(this, instanceProps);

            if (Runfiles != null)
            {
                builder.AppendLine("Runfiles:");
                foreach (var file in Runfiles)
                {
                    builder.Append("\t");
                    builder.AppendLine(file.ToString());
                }
            }

            return builder.ToString();
        }

        public Runfiles GetRunFiles(bool trimWorkspace = true)
        {
            if (Runfiles != null) return Runfiles;
            Runfiles = new Runfiles(File.ReadAllLines(Path.Combine(StartingDirectory, ManifestPath)));
            return Runfiles;
        }

        public void ResolveLabel(Label label)
        {
            label.PathRoot = label.IsRooted ? WorkspaceDirectory : WorkingDirectory;
            if (label.IsPath)
            {
                label.Filepath = Path.IsPathRooted(label.RawValue) ? label.RawValue : Path.Combine(WorkingDirectory, label.RawValue);
            }
            else
            {
                label.Filepath = Path.Combine(label.PathRoot!, label.Package!, label.Name!);
            }
        }
    }

    public class Runfiles : IEnumerable<Runfile>
    {
        public Runfiles(IEnumerable<string> manifestLines)
        {
            foreach (var line in manifestLines)
            {
                var parts = line.Split(' '); // this is safe: ERROR: bazel does not currently work properly from paths containing spaces.
                var file = new Runfile(parts[0], parts[1]);
                Dict[file.BazelPath] = file;
                ShortDict[file.ShortPath] = file;
            }
        }

        public Dictionary<string, Runfile> Dict { get; } = new Dictionary<string, Runfile>();
        public Dictionary<string, Runfile> ShortDict { get; } = new Dictionary<string, Runfile>();

        public IEnumerator<Runfile> GetEnumerator() => Dict.Values.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class Runfile
    {
        public Runfile(string bazelPath, string absolutePath)
        {
            BazelPath = bazelPath;
            AbsolutePath = absolutePath;
            var parts = BazelPath.Split('/', 2);
            WorkspaceName = parts[0];
            ShortPath = parts[1];
        }

        public string BazelPath { get; }
        public string AbsolutePath { get; }
        public string WorkspaceName { get; }
        public string ShortPath { get; }

        public override string ToString()
        {
            return $"{BazelPath}: {AbsolutePath}";
        }
    }
}