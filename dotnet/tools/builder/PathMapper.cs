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

        private static void SetInstance(PathMapper instance)
        {
            _instance = instance;
            InterningBinaryReader.OpportunisticIntern = new PathMappingInterner(instance);
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
            _execRoot = execRoot;
            SetInstance(this);
            
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

        public virtual string ToManifestPath(string absolutePath)
        {
            return _toBazelRegex.Replace(absolutePath, "");
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