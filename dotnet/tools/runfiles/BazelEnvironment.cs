using System;
using System.IO;

namespace MyRulesDotnet.Tools
{
    public static class BazelEnvironment
    {
        public static string GetTestTmpDir()
        {
            // todo(#12) remove Path.GetTempPath()
            var value = Environment.GetEnvironmentVariable("TEST_TMPDIR") ?? Path.GetTempPath();
            if (string.IsNullOrEmpty(value)) throw new IOException("$TEST_TMPDIR is empty or undefined");
            return value;
        }
    }
}
