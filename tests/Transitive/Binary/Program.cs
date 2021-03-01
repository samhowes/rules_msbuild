using System;
using tests.Lib;

namespace tests.Transitive
{
    class Program
    {
        static void Main(string[] args)
        {
            var lib = new LibClass();
            Console.WriteLine($"Hello Transitive: {lib.Value}!");
        }
    }
}
