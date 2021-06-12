using System.Text.RegularExpressions;

namespace RulesMSBuild.Tools.Builder
{
    public class PathReplacer
    {
        private readonly BazelContext _bazel;
        private readonly Regex _regex;

        public PathReplacer(BazelContext bazel)
        {
            _bazel = bazel;

            var execRoot = bazel.ExecRoot[(bazel.OutputBase.Length)..];

            _regex = new Regex($"({Regex.Escape(bazel.OutputBase)})({Regex.Escape(execRoot)})?/",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        public string ReplacePath(string path) => _regex.Replace(path, "");
    }
}