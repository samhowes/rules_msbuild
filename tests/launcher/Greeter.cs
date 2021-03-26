using System;

namespace tests.launcher
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 1)
            {
                Console.WriteLine($"Expected 0 or 1 args, got {args.Length}");
                return;
            }
            if (args.Length == 1) {
                Console.WriteLine($"Hello: {args[0]}!");
            }
            else
            {
                foreach (System.Collections.DictionaryEntry v in Environment.GetEnvironmentVariables())
                {
                    Console.Write($"{v.Key}={v.Value}*~*");
                }
           }
        }
    }
}
