using System;
using System.IO;
using System.Linq;
using Xunit;
using bootstrapper;
using FluentAssertions;

namespace bootstrapper_tests
{
    public class QueryParserTests
    {
        [Fact]
        public void PackagesByTargetFramework_Works()
        {
            var reader = new StringReader(@"<?xml version=""1.1"" encoding=""UTF-8"" standalone=""no""?>
<query version=""2"">
    <rule class=""dotnet_binary"" location=""C:/users/sam/dev/my_rules_dotnet/tests/HelloNuGet/BUILD:3:14"" name=""//tests/HelloNuGet:HelloNuGet"">
        <string name=""name"" value=""HelloNuGet""/>
        <list name=""srcs"">
            <label value=""//tests/HelloNuGet:Program.cs""/>
        </list>
        <string name=""target_framework"" value=""netcoreapp3.1""/>
        <list name=""deps"">
            <label value=""@nuget//commandlineparser:commandlineparser""/>
        </list>
        <rule-input name=""//:dotnet_context_data""/>
        <rule-input name=""//dotnet/private/rules:compile.tpl.proj""/>
        <rule-input name=""//tests/HelloNuGet:Program.cs""/>
        <rule-input name=""@nuget//commandlineparser""/>
    </rule>
</query>
");
            var parser = new QueryParser(reader);

            var results = parser.GetPackagesByFramework("nuget").ToList();
            var only = results.Should().ContainSingle().Which;
            only.Key.Should().Be("netcoreapp3.1");
            var label = only.Value.Should().ContainSingle().Which;
            label.Name.Should().Be("commandlineparser");
        }
    }
}