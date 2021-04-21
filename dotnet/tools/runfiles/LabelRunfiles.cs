using System.Collections.Generic;

namespace MyRulesDotnet.Tools.Bazel
{
    public class LabelRunfiles
    {
        public LabelRunfiles(Runfiles runfiles, string defaultWorkSpace)
        {
            Runfiles = runfiles;
            DefaultWorkspace = defaultWorkSpace;
        }

        public Runfiles Runfiles { get; }
        public string DefaultWorkspace { get; }
        public Dictionary<string, string> GetEnvVars() => Runfiles.GetEnvVars();

        public string Rlocation(Label label)
        {
            var workspace = label.Workspace == Label.DefaultWorkspace ? DefaultWorkspace : label.Workspace;
            var rpath = $"{workspace}/{label.RelativeRpath}";
            return Runfiles.Rlocation(rpath);
        }

        public void SetEnvVars(IDictionary<string, string> env) => Runfiles.SetEnvVars(env);
    }
}