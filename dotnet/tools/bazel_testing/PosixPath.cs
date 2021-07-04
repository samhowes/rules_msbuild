#nullable enable
using System.Text;

namespace TestRunner
{
    public static class PosixPath
    {
        public const char Separator = '/';
        public static string? GetDirectoryName(string path)
        {
            var parts = path.Split(Separator);
            if (parts.Length == 1) return null;
            return string.Join(Separator, parts[..^1]);
        }

        public static string Combine(params string[] parts)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];

                if (i > 0 && part.StartsWith(Separator))
                    part = part[1..];

                builder.Append(part);
                if (i < parts.Length -1 && !part.EndsWith(Separator))
                    builder.Append(Separator);
            }

            return builder.ToString();
        }

        public static string GetFileName(string path)
        {
            var last = path.LastIndexOf(Separator);
            if (last < 0)
                last = 0;
            else
                last++;
            return path[last..];
        }
    }
}
