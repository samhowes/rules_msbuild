using System;
using System.IO;

namespace Bzl
{
    public static class Files
    {
        public static void Walk(string path, Func<string, bool, bool> callback)
        {
            foreach (var file in Directory.EnumerateFiles(path))
            {
                if (!callback(file, false)) return;
            }
            
            foreach (var directory in Directory.EnumerateDirectories(path))
            {
                if (!callback(directory, true)) return;
                Walk(directory, callback);
            }
        }
    }
}