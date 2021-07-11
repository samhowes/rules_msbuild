using System;
using System.IO;
using System.Text.RegularExpressions;

namespace RulesMSBuild.Tools.Builder
{
    public class PathMapper
    {
        private readonly string _outputBase;
        private readonly string _execRoot;
        const string OutputBase = "$output_base";
        const string ExecRoot = "$exec_root";
        private readonly Regex _toBazelRegex;
        private readonly Regex _fromBazelRegex;
        protected PathMapper(){} // for Moq

        public PathMapper(string outputBase, string execRoot)
        {
            if (!execRoot.StartsWith(outputBase))
                throw new ArgumentException($"Unexpected output_base<>exec_root combination: {outputBase}<>{execRoot}");
            _outputBase = outputBase;
            _execRoot = execRoot;
            
            var execRootSegment = execRoot[(outputBase.Length)..];

            _toBazelRegex = new Regex(@$"({Regex.Escape(outputBase)})({Regex.Escape(execRootSegment)})?(/|\\)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
            
            _fromBazelRegex = new Regex($"({Regex.Escape(OutputBase)})|({Regex.Escape(ExecRoot)})",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        public virtual string ToBazel(string path) => _toBazelRegex.Replace(path, (match) =>
        {
            return (match.Groups[2].Success ? ExecRoot : OutputBase) + match.Groups[3].Value;
        });
        
        public virtual string FromBazel(string path) => _fromBazelRegex.Replace(path, (match) =>
        {
            return match.Groups[2].Success ? _execRoot : _outputBase; 
        });

        public string ToManifestPath(string absolutePath)
        {
            return _toBazelRegex.Replace(absolutePath, "");
        }

        public string ToAbsolute(string manifestPath)
        {
            return Path.Combine(_execRoot, manifestPath);
        }
    }
}