using System.Collections.Generic;

namespace MyRulesDotnet.Tools.Bazel
{
    public class LabelRunfiles
    {
        public LabelRunfiles(Runfiles runfiles, Label defaultPackage)
        {
            Runfiles = runfiles;
            DefaultPackage = defaultPackage;
        }

        public Runfiles Runfiles { get; }
        public Label DefaultPackage { get; set; }
        public Dictionary<string, string> GetEnvVars() => Runfiles.GetEnvVars();

        public string PackagePath(string subpath)
        {
            if (subpath[0] == ':')
            {
                subpath = subpath[1..];
            }
            var rpath = $"{DefaultPackage.Workspace}/{DefaultPackage.Package}/{subpath}";
            return Runfiles.Rlocation(rpath);
        }
        public string Rlocation(Label label)
        {
            var workspace = label.Workspace == Label.DefaultWorkspace ? DefaultPackage.Workspace : label.Workspace;
            var rpath = $"{workspace}/{label.RelativeRpath}";
            return Runfiles.Rlocation(rpath);
        }

        public void SetEnvVars(IDictionary<string, string> env) => Runfiles.SetEnvVars(env);
    }
}