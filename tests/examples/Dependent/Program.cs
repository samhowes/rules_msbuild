using System;
using tests.ClassLibrary;

namespace tests.Dependent
{
    class Program
    {
        static void Main(string[] args)
        {
            var secret = new Secret();
            Console.WriteLine($"Hello Secret: {secret.Value}");
        }
    }
}
