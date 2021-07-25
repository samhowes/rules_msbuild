using System.Collections.Generic;

namespace RulesMSBuild.Tools.Builder.Caching
{
    public class CacheManifest
    {
        public class BuildResultCache
        {
            public string Project { get; set; }
            public string Result { get; set; }
        }

        public BuildResultCache Output { get; set; } = null!;
        public Dictionary<string, string> Projects { get; set; } = null!;
        public List<string> Results { get; set; } = new List<string>();
    }
}