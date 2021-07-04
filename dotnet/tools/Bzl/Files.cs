using System;
using System.Diagnostics;
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

        public static void PostOrderWalk(string path, Func<DirectoryInfo,bool> beforeVisitDirectory,  Action<FileSystemInfo> afterVisit)
        {
            var info = new DirectoryInfo(path);
            PostOrderWalkImpl(info, beforeVisitDirectory, afterVisit);
        }

        private static void PostOrderWalkImpl(DirectoryInfo directory, Func<DirectoryInfo, bool> beforeVisitDirectory, Action<FileSystemInfo> afterVisit)
        {
            foreach (var sub in directory.EnumerateDirectories())
            {
                if (beforeVisitDirectory(sub))
                    PostOrderWalkImpl(sub, beforeVisitDirectory, afterVisit);

                afterVisit(sub);
            }

            foreach (var file in directory.EnumerateFiles())
            {
                afterVisit(file);
            }
        }
    }
}
