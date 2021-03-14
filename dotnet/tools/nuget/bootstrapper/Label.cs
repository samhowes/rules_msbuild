#nullable enable

using System.Text.RegularExpressions;

namespace bootstrapper
{
    public class Label
    {
        private static Regex LabelRegex = new Regex("^(@(?<repository>\\w+))?(?<root>//)?(?<package>[^\\:]+)?:?(?<name>.*)");

        public Label(string labelOrPath)
        {
            RawValue = labelOrPath;
            var match = LabelRegex.Match(labelOrPath);
            if (match.Success)
            {
                WorkspaceName = match.Groups["repository"].Value;
                Repository = match.Groups["repository"].Value;
                IsRooted = match.Groups["root"].Success;
                Package = match.Groups["package"].Value ?? "";
                Name = match.Groups["name"].Value;
            }
            else
            {
                IsPath = true;
            }
        }

        public string RawValue { get; set; }

        public bool IsPath { get; set; }

        public string? WorkspaceName { get; set; }

        public bool IsRooted { get; set; }

        public string? Repository { get; set; }

        public string? Name { get; set; }
        public string? FullName => "@" + WorkspaceName + "//" + Package + ":" + Name;
        public string? Package { get; set; }

        public string? PathRoot { get; set; }

        public string? Filepath { get; set; }
    }
}