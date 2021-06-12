using System;
using Xunit;

namespace RulesMSBuild.Tests.Examples.HelloTest
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
