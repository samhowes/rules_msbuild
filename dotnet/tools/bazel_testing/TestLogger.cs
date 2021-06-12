using System;

namespace TestRunner
{
    public static class TestLogger
    {
        public static bool DebugEnabled { get; set; } = true;
        public static void Debug(string message)
        {
            if (!DebugEnabled) return;
            Console.WriteLine($"[debug] {message}");
        }

        public static void Info(string message)
        {
            Console.WriteLine(message);
        }
        public static bool Fail(string message)
        {
            Console.Error.WriteLine(message);
            Console.Error.Flush();
            Environment.Exit(1);
            return false;
        }
    }
}