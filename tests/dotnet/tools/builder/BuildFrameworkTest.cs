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
            RulesMSBuild.Tools.Builder.Program.RegisterSdk("/usr/local/share/dotnet/sdk/5.0.203");
        }
    }
}