using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;

namespace RulesMSBuild.Tools.Builder.Caching
{
    public class LabelResult : ITranslatable
    {
        public Label Label;
        public ConfigCache ConfigCache;
        // public ResultsCache ResultsCache;
        public IDictionary<int, string> ConfigMap = new Dictionary<int, string>();
        public Dictionary<int, int> NewIds; // do not translate
        public IDictionary<int, int> OriginalIds;
        public BuildResult[] Results;

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref Label);
            translator.Translate(ref ConfigCache);
            translator.TranslateArray(ref Results);

            translator.TranslateDictionary(ref ConfigMap,
                (ITranslator t, ref int i) => t.Translate(ref i),
                (ITranslator t, ref string s) => t.Translate(ref s),
                c => new Dictionary<int, string>()
            );
            translator.TranslateDictionary(ref OriginalIds,
                (ITranslator t, ref int i) => t.Translate(ref i),
                (ITranslator t, ref int i) => t.Translate(ref i),
                c => new Dictionary<int, int>()
            );
        }
    }
}