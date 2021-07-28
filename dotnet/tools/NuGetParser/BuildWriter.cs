using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NuGetParser
{
    public class BuildWriter : IDisposable
    {
        private readonly StreamWriter _writer;
        private readonly JsonSerializerOptions _jsonBzlOptions = new JsonSerializerOptions()
        {
            WriteIndented = true, 
            AllowTrailingCommas = true, 
            // don't encode plus signs into unicode sequences
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
        };
        private static readonly Regex FormattingRegex = new Regex(@"\n.", RegexOptions.Compiled | RegexOptions.Multiline);

        public string BzlValue<T>(T value, string prefix = "    ")
        {
            var json = JsonSerializer.Serialize(value, _jsonBzlOptions);
            // this indents with two spaces, and the indent is not customizable
            // the below replacement will still end up with a little weird formatting, but
            // using system.text.json means we don't have to deal with requesting a version of Newtonsoft.Json
            return FormattingRegex.Replace(json, match =>
            {
                var v = match.Value;
                if (v[1] == ']')
                {
                    // expand no spaces to 4
                    return v[0] + prefix + ']';
                }

                // expand 2 spaces to 4
                return v[0] + prefix + "  " + v[1];
            });
        }
    
        public BuildWriter(Stream stream)
        {
            _writer = new StreamWriter(stream);
        }

        public void Load(string bzlSource, params string[] rules)
        {
            _writer.WriteLine($@"load(""{bzlSource}"", ""{string.Join("\", \"", rules)}"")");
        }

        public void Call(string functionName, params (string, object)[] namedArgs)
        {
            _writer.Write(functionName);
            _writer.Write("(");
            var indent = 0;
            if (namedArgs.Length > 1)
            {
                indent = 1;
                _writer.WriteLine();
            }

            for (var index = 0; index < namedArgs.Length; index++)
            {
                var (name, value) = namedArgs[index];
                _writer.Write(name);
                _writer.Write(" = ");
                switch (value)
                {
                    case string s:
                        Quote(s);
                        break;
                    default:
                        _writer.Write(value);
                        break;
                }
                if (index < namedArgs.Length -1)
                    _writer.Write(",");
                if (indent > 0)
                    _writer.WriteLine();
            }
            _writer.WriteLine(")");
        }

        public void Quote(string s) => _writer.Write($"\"{s}\"");
        
        public void Visibility()
        {
            _writer.WriteLine();
            _writer.WriteLine(@"package(default_visibility = [""//visibility:public""])");
            _writer.WriteLine();
        }

        public void StartRule(string type, string name)
        {
            _writer.WriteLine($@"{type}(");
            SetAttr("name", name);
        }

        public void SetAttr(string propertyName, string value)
        {
            _writer.WriteLine($@"    {propertyName} = ""{value}"",");
        }
        public void SetAttrRaw(string propertyName, string value)
        {
            _writer.WriteLine($@"    {propertyName} = {value},");
        }
        
        public void SetAttr<T>(string propertyName, T value)
        {
            _writer.Write($@"    {propertyName} = ");
            _writer.Write(BzlValue(value));
            _writer.WriteLine(",");
        }

        public void Dispose()
        {
            _writer?.Dispose();
        }

        public void EndRule()
        {
            _writer.WriteLine(")");
            _writer.WriteLine();
        }

        public void Raw(string s)
        {
            _writer.WriteLine(s);
            _writer.WriteLine();
        }

        public void InlineCall(string functionName, string value)
        {
            _writer.Write($"{functionName}(");
            _writer.Write(value);
            _writer.WriteLine(")");
            _writer.WriteLine();
        }
    }
}