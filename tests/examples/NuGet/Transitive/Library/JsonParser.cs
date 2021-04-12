using System;
using Newtonsoft.Json;

namespace MyRulesDotnet.Tests.NuGet.Transitive
{
    public class MyObject
    {
        public string Foo;
    }

    public class JsonParser
    {
        public static MyObject Parse(string json)
        {
            return JsonConvert.DeserializeObject<MyObject>(json);
        }
    }
}
