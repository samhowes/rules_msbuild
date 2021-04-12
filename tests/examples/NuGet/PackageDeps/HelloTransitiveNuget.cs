using System;
using Xunit;

namespace MyRulesDotnet.Tests.Examples.NuGet.Transitive
{
    public class Program
    {
        public static void Main() {
            Console.WriteLine("hello");
        }
    }

    public abstract class TransitiveTestBase
    {
        [Fact]
        public void HelloTest()
        {
            Assert.Equal(1 + 1, 2);
        }
    }
}
