using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build;
using Microsoft.Build.BackEnd;
using RulesMSBuild.Tools.Builder.MSBuild;

namespace RulesMSBuild.Tools.Builder
{
    public class PathMapper
    {
        private static PathMapper? _instance;

        private readonly string _outputBase;
        private readonly string _execRoot;
        const string OutputBase = "$output_base";
        const string ExecRoot = "$exec_root";
        private readonly Regex _toBazelRegex;
        private readonly Regex _fromBazelRegex;
        private readonly string _externalPrefix;

        private static void SetInstance(PathMapper instance)
        {
            _instance = instance;
            InterningBinaryReader.Strings = new PathMappingInterner(instance);
            BinaryTranslator.BinaryWriteTranslator.BinaryWriterFactory =
                (stream) => new PathMappingBinaryWriter(stream, instance);
        }

        // for Moq
        protected PathMapper()
        {
            SetInstance(this);
        }

        public PathMapper(string outputBase, string execRoot)
        {
            if (_instance != null)
                throw new InvalidOperationException("There can only be one PathMapper instantiated at a time.");
            if (!execRoot.StartsWith(outputBase))
                throw new ArgumentException($"Unexpected output_base<>exec_root combination: {outputBase}<>{execRoot}");
            _outputBase = outputBase;
            // bazel invokes us at $output_base/sandbox/darwin-sandbox/17/execroot/<workspace_name>
            // this code needs the actual folder named "execroot" not the folder that the bazel docs call "execRoot"
            // which is "Working tree for the Bazel build & root of symlink forest: execRoot"
            // https://docs.bazel.build/versions/main/output_directories.html
            _execRoot = Path.GetDirectoryName(execRoot)!;
            var hostWorkspace = execRoot[(_execRoot.Length + 1)..];
            _externalPrefix = hostWorkspace + "/external";
            SetInstance(this);

            var execRootSegment = _execRoot[(outputBase.Length)..];

            _toBazelRegex = new Regex(
                @$"({Regex.Escape(outputBase)})({Regex.Escape(execRootSegment)})?(/|\\)([^\\/]+(/|\\))?",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

            _fromBazelRegex = new Regex($"({Regex.Escape(OutputBase)})|({Regex.Escape(ExecRoot)})",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        public virtual string ToBazel(string path) => _toBazelRegex.Replace(path,
            (match) =>
            {
                return (match.Groups[2].Success ? ExecRoot : OutputBase) + match.Groups[3].Value +
                       match.Groups[4].Value;
            });

        public virtual string ToRelative(string path) => _toBazelRegex.Replace(path, (match) => "");

        public virtual string FromBazel(string path) => _fromBazelRegex.Replace(path,
            (match) => { return match.Groups[2].Success ? _execRoot : _outputBase; });

        public virtual string ToManifestPath(string absolutePath)
        {
            var path = _toBazelRegex.Replace(absolutePath, "$4")
                // even on non-windows, MSBuild still uses backslashes sometimes.
                .Replace('\\', '/');

            if (path.StartsWith(_externalPrefix))
            {
                path = path.Substring(_externalPrefix.Length + 1);
            }

            return path;
        }

        public virtual string ToAbsolute(string manifestPath)
        {
            return Path.Combine(_execRoot, manifestPath);
        }

        public static void ResetInstance()
        {
            _instance = null;
        }
    }
}