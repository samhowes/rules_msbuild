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
        private StringReader _reader;
        private QueryParser _parser;

        public QueryParserTests()
        {
            _reader = new StringReader(@"<?xml version=""1.1"" encoding=""UTF-8"" standalone=""no""?>
<query version=""2"">
    <rule class=""dotnet_binary"" location=""C:/users/sam/dev/my_rules_dotnet/tests/HelloNuGet/BUILD:3:14"" name=""//tests/HelloNuGet:HelloNuGet_pre_restore"">
        <string name=""name"" value=""HelloNuGet_pre_restore""/>
        <list name=""srcs"">
            <label value=""//tests/HelloNuGet:Program.cs""/>
        </list>
        <string name=""target_framework"" value=""netcoreapp3.1""/>
        <list name=""deps"">
            <label value=""@nuget//commandlineparser:commandlineparser""/>
        </list>
        <rule-input name=""//:dotnet_context_data""/>
        <rule-input name=""//dotnet/private/msbuild:compile.tpl.proj""/>
        <rule-input name=""//tests/HelloNuGet:Program.cs""/>
        <rule-input name=""@nuget//commandlineparser""/>
    </rule>
</query>
");
            _parser = new QueryParser(_reader);
        }

        [Fact]
        public void PackagesByTargetFramework_Works()
        {
            var results = _parser.GetPackagesByFramework("nuget").ToList();
            var only = results.Should().ContainSingle().Which;
            only.Key.Should().Be("netcoreapp3.1");
            var label = only.Value.Should().ContainSingle().Which;
            label.Name.Should().Be("commandlineparser");
        }

        [Fact]
        public void GetTargets_Works()
        {
            var results = _parser.GetTargets("my_rules_dotnet");

            var result = results.Should().ContainSingle().Subject;
            result.Label.RawValue.Should().Be("//tests/HelloNuGet:HelloNuGet_pre_restore");
            result.Label.WorkspaceName.Should().Be("my_rules_dotnet");
        }
    }
}