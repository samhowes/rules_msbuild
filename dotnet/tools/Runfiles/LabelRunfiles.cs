using System.Collections.Generic;

namespace RulesMSBuild.Tools.Bazel
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
            if (label.IsRelative) return PackagePath(label.Name);
            var rpath = Rpath(label);
            return Runfiles.Rlocation(rpath);
        }

        private string Rpath(Label label)
        {
            var workspace = label.Workspace == Label.DefaultWorkspace ? DefaultPackage.Workspace : label.Workspace;
            var rpath = $"{workspace}/{label.RelativeRpath}";
            return rpath;
        }

        public void SetEnvVars(IDictionary<string, string> env) => Runfiles.SetEnvVars(env);

        public IEnumerable<string> ListRunfiles(Label label)
        {
            var rpath = Rpath(label);
            return Runfiles.ListRunfiles(rpath);
        }
    }
}
