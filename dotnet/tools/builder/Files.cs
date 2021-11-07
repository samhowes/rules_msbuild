using System.Collections.Generic;
using System.IO;

namespace RulesMSBuild.Tools.Builder
{
    public class Files
    {
        public virtual string GetContents(string path) => File.ReadAllText(path);
        public virtual void WriteContents(string path, string contents) => File.WriteAllText(path, contents);
        public virtual IEnumerable<string> GetFiles(string path) => Directory.EnumerateFiles(path);
        public virtual IEnumerable<string> GetDirectories(string path) => Directory.EnumerateDirectories(path);
        public virtual Stream Create(string path) => File.Create(path);
        public virtual Stream OpenRead(string path) => File.OpenRead(path);

        public virtual bool Exists(string path) => File.Exists(path);
    }

    public class Paths
    {
        public Paths(char directorySeparatorChar = default)
        {
            DirectorySeparatorChar =
                directorySeparatorChar == default ? Path.DirectorySeparatorChar : directorySeparatorChar;
        }

        public virtual char DirectorySeparatorChar { get; }
        public virtual string Join(params string[] paths) => string.Join(DirectorySeparatorChar, paths);
        public virtual string Combine(params string[] paths) => Path.Combine(paths);
        public virtual string GetRelativePath(string relativeTo, string path) => Path.GetRelativePath(relativeTo, path);
    }

    public class SimplerPaths : Paths
    {
        private static readonly char[] AllSeparators = {'\\', '/'};
        public override string Combine(params string[] paths) => Fix(Join(paths));

        public override string GetRelativePath(string relativeTo, string path)
        {
            var relative = Path.GetRelativePath(relativeTo, path);
            return Fix(relative);
        }

        public string Fix(string path)
        {
            if (DirectorySeparatorChar != Path.DirectorySeparatorChar)
                return path.Replace(Path.DirectorySeparatorChar, DirectorySeparatorChar);
            return path;
        }

        public string Unfix(string path)
        {
            if (DirectorySeparatorChar != Path.DirectorySeparatorChar)
                return path.Replace(DirectorySeparatorChar, Path.DirectorySeparatorChar);
            return path;
        }
    }

    public class WindowsPaths : SimplerPaths
    {
        public override char DirectorySeparatorChar => '\\';

        public override string GetRelativePath(string relativeTo, string path)
        {
            if (Path.DirectorySeparatorChar != DirectorySeparatorChar)
            {
                string root = "";
                if (relativeTo.Length >= 2 && relativeTo[1] == ':')
                {
                    root = relativeTo[..2];
                    relativeTo = relativeTo[2..];
                }

                return root + Unfix(Path.GetRelativePath(Fix(relativeTo), Fix(path)));
            }

            return Path.GetRelativePath(relativeTo, path);
        }
    }
}