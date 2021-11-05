using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RulesMSBuild.Tools.Builder.Launcher
{
    public class LaunchDataWriter : IDisposable
    {
        private readonly Stream _stream;
        private List<(string, string)> _data;

        public LaunchDataWriter(Stream stream)
        {
            _stream = stream;
            _data = new List<(string, string)>();
        }

        public LaunchDataWriter Add(string key, string value)
        {
            _data.Add((key,value));
            return this;
        }

        public void Save()
        {
            var launchDataStart = _stream.Position;

            // see @bazel_tools//src/tools/launcher/util/data_parser.cc
            // a single key-value pair is stored as single set of non-null bytes
            // the '{key}=' segment is read into a single-byte `char` (ascii)
            // the '{value}' segment is read into a two-byte "wide char" `wchar_t`
            // https://docs.microsoft.com/en-us/cpp/cpp/char-wchar-t-char16-t-char32-t?view=msvc-160
            // https://stackoverflow.com/a/402918/2524934
            var encoding = Encoding.UTF8;

            foreach (var (key, value) in _data)
            {
                var builder = new StringBuilder(key.Length + 2 + value.Length)
                    .Append(key)
                    .Append('=')
                    .Append(value)
                    .Append('\0');

                _stream.Write(encoding.GetBytes(builder.ToString()));
            }

            Int64 launchDataLength = _stream.Position - launchDataStart;
            _stream.Write(BitConverter.GetBytes(launchDataLength));
            _stream.Flush();
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}