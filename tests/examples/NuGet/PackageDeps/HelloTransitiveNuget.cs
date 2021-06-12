using System;
using Xunit;

namespace RulesMSBuild.Tests.Examples.NuGet.Transitive
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
            Assert.Equal(2, 1 + 1);
        }
    }
}
