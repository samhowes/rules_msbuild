using System;
using System.IO;

namespace MyRulesDotnet.Tools.Bazel
{
    public static class BazelEnvironment
    {
        public static string GetTestTmpDir()
        {
            var value = Environment.GetEnvironmentVariable("TEST_TMPDIR");
            if (string.IsNullOrEmpty(value)) throw new IOException("$TEST_TMPDIR is empty or undefined");
            return value;
        }
    }
}
