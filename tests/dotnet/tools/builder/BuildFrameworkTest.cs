using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        static BuildFrameworkFixture()
        {
            SdkRoot = Environment.GetEnvironmentVariable("BAZEL_DOTNET_SDKROOT");
            if (SdkRoot == null)
            {
                var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");

                SdkRoot = Directory.GetDirectories(Path.Combine(dotnetRoot!, "sdk"))
                    .Last(d => Path.GetFileName(d).StartsWith("5"));
            }
            else
            {
                SdkRoot = Path.GetFullPath(SdkRoot);
            }
        }

        public static string SdkRoot { get; set; }

        public BuildFrameworkFixture()
        {
            RulesMSBuild.Tools.Builder.Program.RegisterSdk(SdkRoot);
        }
    }
}
