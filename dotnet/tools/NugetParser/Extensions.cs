using System;
using System.Collections.Generic;
using System.Text.Json;

namespace NuGetParser
{
    public static class JsonElementExtensions
    {
        public static bool GetRequired(this JsonElement e, string name, out JsonElement value)
        {
            if (!e.TryGetProperty(name, out value))
            {
                throw new Exception($"Missing required property: {name}");
                return false;
            }

            return true;
        }
    }
    
    public static class DictionaryExtensions
    {
        public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> d, TKey k, Func<TValue> vf)
        {
            if (!d.TryGetValue(k, out var v))
            {
                v = vf();

                d[k] = v;
            }

            return v;
        }
    }
}