using System;
using System.IO;
using System.Text;
using Microsoft.Build;
using Microsoft.NET.StringTools;
using RulesMSBuild.Tools.Builder.Diagnostics;

namespace RulesMSBuild.Tools.Builder.MSBuild
{
    public class PathMappingInterner : ICustomInterner
    {
        private readonly PathMapper _pathMapper;

        public PathMappingInterner(PathMapper pathMapper)
        {
            _pathMapper = pathMapper;
        }

        public string WeakIntern(string str)
        {
            return Strings.WeakIntern(_pathMapper.FromBazel(str));
        }

        public string WeakIntern(ReadOnlySpan<char> str)
        {
            return Strings.WeakIntern(_pathMapper.FromBazel(new string(str)));
        }
    }

    public class PathMappingBinaryWriter : BinaryWriter
    {
        private readonly PathMapper _pathMapper;

        public PathMappingBinaryWriter(Stream stream, PathMapper pathMapper) : base(stream)
        {
            _pathMapper = pathMapper;
        }

        public override void Write(string value)
        {
            base.Write(_pathMapper.ToBazel(value));
        }
    }
}