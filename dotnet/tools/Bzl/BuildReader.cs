using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Bzl
{
    public class BuildReader
    {
        private readonly StreamReader _reader;
        private static Regex MarkerRegex = new Regex(@"#\s?bzl:(?<name>\w+)\s(?<position>start|end)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public BuildReader(Stream stream)
        {
            _reader = new StreamReader(stream);
        }

        public string GetUntilMarker(string markerName)
        {
            return ReadUntilPosition(markerName, "start");
        }

        private string ReadUntilPosition(string markerName, string position)
        {
            var builder = new StringBuilder();
            for (;;)
            {
                var line = _reader.ReadLine();
                if (line == null) break;
                if (IsMarkerPosition(line, markerName, position)) break;

                builder.AppendLine(line);
            }

            return builder.ToString();
        }

        private static bool IsMarkerPosition(string line, string markerName, string markerPosition)
        {
            var match = MarkerRegex.Match(line);
            if (match.Success)
            {
                if (string.Equals(match.Groups["name"].Value, markerName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(match.Groups["position"].Value, markerPosition))
                {
                    return true;
                }
            }

            return false;
        }

        public void SkipToEnd(string markerName)
        {
            ReadUntilPosition(markerName, "end");
        }

        public string ReadAll()
        {
            return ReadUntilPosition("_______", "_____"); // whatever
        }

        public void Dispose()
        {
            _reader.Dispose();
        }
    }
}