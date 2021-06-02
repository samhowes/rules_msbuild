using System;
using System.IO;

namespace MyRulesDotnet.Tools.Bazel
{
    public static class BazelEnvironment
    {
        private static Random _rnd = new Random();
        public static string GetTmpDir()
        {
            var value = Environment.GetEnvironmentVariable("TEST_TMPDIR");
            if (!string.IsNullOrEmpty(value)) return value;
            return Path.GetTempPath();
        }

        public static string GetTmpDir(string prefix)
        {
            prefix += "_";
            var baseTmp = GetTmpDir();
            for (int i = 0; i < 1000; i++)
            {
                var num = _rnd.Next(1000, 9999);
                var path = Path.Combine(baseTmp, prefix + num.ToString());
                if (File.Exists(path)) continue;
                Directory.CreateDirectory(path);
                return path;
            }

            throw new Exception(
                $"Failed to find a unique path after 1000 attempts. Maybe you need to clean the temp directory?");
        }
    }
}
