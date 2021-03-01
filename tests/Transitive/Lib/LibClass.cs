using System;
using tests.TransitiveLib;

namespace tests.Lib 
{
    public class LibClass
    {
        public string Value { get; }
        public LibClass()
        {
            var transitive = new TransitiveLibClass();
            Value = transitive.Value;
        }
    }
}