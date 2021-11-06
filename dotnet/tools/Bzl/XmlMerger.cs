using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Bzl
{
    public class XmlMerger
    {
        private static readonly Regex MarkerRegex = new Regex(
            @"(<!--(\s+)?bzl:(?<name>\w+)\s(?<position>start|end)(\s+)?-->)|(?<end></Project>)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly StreamReader _reader;
        private readonly StringWriter _writer;

        public XmlMerger(Stream original)
        {
            _reader = new StreamReader(original);
            _writer = new StringWriter();
        }

        private enum MarkerState
        {
            NotFound,
            Found,
            EOF
        }

        public string Replace(string markerName, string contents)
        {
            var addExtraLine = ReadUntilPosition(markerName, "start", out var header);
            _writer.Write(header);
            if (!addExtraLine)
                _writer.WriteLine();

            _writer.WriteLine($"    <!--  bzl:{markerName} start  -->");
            _writer.Write(contents);
            _writer.WriteLine($"    <!--  bzl:{markerName} end  -->");
            if (!ReadUntilPosition(markerName, "end", out var toCopy))
            {
                _writer.WriteLine(toCopy);
                _writer.WriteLine("</Project>");
            }
            else
            {
                ReadUntilPosition("___", "___", out var footer);
                _writer.Write(footer);
            }

            _writer.Flush();
            return _writer.GetStringBuilder().ToString();
        }

        private bool ReadUntilPosition(string markerName, string position, out string contents)
        {
            bool found = false;
            var builder = new StringBuilder();
            for (;;)
            {
                var line = _reader.ReadLine();
                if (line == null) break;
                var state = IsMarkerPosition(line, markerName, position);
                if (state == MarkerState.Found)
                {
                    found = true;
                    break;
                }

                builder.AppendLine(line);
                if (state == MarkerState.EOF) break;
            }

            contents = builder.ToString();
            return found;
        }

        private static MarkerState IsMarkerPosition(string line, string markerName, string markerPosition)
        {
            var match = MarkerRegex.Match(line);
            if (match.Success)
            {
                if (string.Equals(match.Groups["name"].Value, markerName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(match.Groups["position"].Value, markerPosition))
                {
                    return MarkerState.Found;
                }

                if (match.Groups["end"].Success) return MarkerState.EOF;
            }

            return MarkerState.NotFound;
        }
    }
}