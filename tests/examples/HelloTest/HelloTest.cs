using System;
using Xunit;

namespace MyRulesDotnet.Tests.Examples.HelloTest
{
    public class HelloTestTest
    {
        [Fact]
        public void HelloTest()
        {
            Assert.Equal(1 + 1, 2);
        }
    }
}
