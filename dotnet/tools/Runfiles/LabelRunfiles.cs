using System.Collections.Generic;

namespace MyRulesDotnet.Tools.Bazel
{
    public class LabelRunfiles
    {
        public LabelRunfiles(Runfiles runfiles, string defaultWorkSpace, string defaultPackage)
        {
            Runfiles = runfiles;
            DefaultWorkspace = defaultWorkSpace;
            DefaultPackage = defaultPackage;
        }

        public Runfiles Runfiles { get; }
        public string DefaultWorkspace { get; }
        public string DefaultPackage { get; set; }
        public Dictionary<string, string> GetEnvVars() => Runfiles.GetEnvVars();

        public string PackagePath(string subpath)
        {
            var rpath = $"{DefaultPackage}/{DefaultPackage}/{subpath}";
            return Runfiles.Rlocation(rpath);
        }
        public string Rlocation(Label label)
        {
            var workspace = label.Workspace == Label.DefaultWorkspace ? DefaultWorkspace : label.Workspace;
            var rpath = $"{workspace}/{label.RelativeRpath}";
            return Runfiles.Rlocation(rpath);
        }

        public void SetEnvVars(IDictionary<string, string> env) => Runfiles.SetEnvVars(env);
    }
}