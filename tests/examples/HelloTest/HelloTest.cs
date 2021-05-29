using System;
using Xunit;

namespace MyRulesDotnet.Tests.Examples.HelloTest
{
    public class HelloTestTest
    {
        [Fact]
        public void HelloTest()
        {
            Assert.Equal(2, 1 + 1);
        }
    }
}
