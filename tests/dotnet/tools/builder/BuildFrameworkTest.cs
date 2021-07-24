using System;
using RulesMSBuild.Tools.Builder;
using Xunit;

namespace RulesMSBuild.Tests.Tools
{
    [CollectionDefinition(TestCollectionName)]
    public class BuildFrameworkTestCollection : ICollectionFixture<BuildFrameworkFixture>
    {
        public const string TestCollectionName = "Build Frameworks Collection";
    }

    public class BuildFrameworkFixture
    {
        public BuildFrameworkFixture()
        {
            var sdkRoot = Environment.GetEnvironmentVariable("BAZEL_DOTNET_SDKROOT") ??
                          "/usr/local/share/dotnet/sdk/5.0.203";
            RulesMSBuild.Tools.Builder.Program.RegisterSdk(sdkRoot);
        }
    }
}