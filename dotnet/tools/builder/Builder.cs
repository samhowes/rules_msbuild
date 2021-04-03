
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MyRulesDotnet.Tools.Builder
{
    class Program
    {
        static void Main(string[] args)
        {
            // var r = Runfiles.Create();
            var factory = new LauncherFactory();
            factory.Create(args);
        }
    }

    /// <summary>
    /// https://docs.google.com/document/d/1z6Xv95CJYNYNYylcRklA6xBeesNLc54dqXfri0z0e14/edit#heading=h.ehp217t4xp3w
    /// </summary>
    public class LauncherFactory
    {
        // private readonly Runfiles _runfiles;

        public LauncherFactory()
        {
            // _runfiles = runfiles;
        }

        public void Create(string[] args)
        {
            var launcherTemplate = new FileInfo(args[0]);
            if (!launcherTemplate.Exists)
                throw new Exception($"Launcher template does not exist at '{args[0]}'");

            using var output = new FileInfo(args[1]).OpenWrite();
            using (var input = launcherTemplate.OpenRead())
            {
                input.CopyTo(output);
            }

            var writer = new LaunchDataWriter()
                // see: //dotnet/tools/launcher/windows:launcher_main.cc
                .Add("binary_type", "Dotnet");

            for (int i = 2; i + 1 < args.Length; i++)
            {
                writer.Add(args[i], args[i + 1]);
            }

            writer.Write(output);
        }
    }

    public class LaunchDataWriter
    {
        private List<(string, string)> _data;

        public LaunchDataWriter()
        {
            _data = new List<(string, string)>();
        }

        public LaunchDataWriter Add(string key, string value)
        {
            _data.Add((key,value));
            return this;
        }

        public void Write(Stream stream)
        {
            var launchDataStart = stream.Position;

            // see @bazel_tools//src/tools/launcher/util/data_parser.cc
            // a single key-value pair is stored as single set of non-null bytes
            // the '{key}=' segment is read into a single-byte `char` (ascii)
            // the '{value}' segment is read into a two-byte "wide char" `wchar_t`
            // https://docs.microsoft.com/en-us/cpp/cpp/char-wchar-t-char16-t-char32-t?view=msvc-160
            // https://stackoverflow.com/a/402918/2524934
            var encoding = Encoding.UTF8;

            foreach (var (key, value) in _data)
            {
                var builder = new StringBuilder(key.Length + 1 + value.Length)
                    .Append(key)
                    .Append('=')
                    .Append(value)
                    .Append('\0');

                stream.Write(encoding.GetBytes(builder.ToString()));
            }

            Int64 launchDataLength = stream.Position - launchDataStart;
            stream.Write(BitConverter.GetBytes(launchDataLength));
            stream.Flush();
        }
    }
}
