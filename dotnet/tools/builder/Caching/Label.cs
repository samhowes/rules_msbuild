using Microsoft.Build.BackEnd;

namespace RulesMSBuild.Tools.Builder.Caching
{
    /// <summary>
    /// The BazelLabel class can't have anything to do with ITranslatable because it needs to be usable before the
    /// msbuild assemblies have been loaded.
    /// </summary>
    public class Label : BazelContext.BazelLabel, ITranslatable
    {
        public Label(string workspace, string package, string name)
            : base(workspace, package, name)
        {}

        public Label(){} // constructor for deserialization

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref Workspace);
            translator.Translate(ref Package);
            translator.Translate(ref Name);
        }
    }
}