namespace MyRulesDotnet.Tools.Bazel
{
    public class Label
    {
        public Label(string rawValue)
        {
            RawValue = rawValue;
            IsValid = Parse();
            if (!IsValid) return;

            RelativeRpath = Package == "" ? Target : $"{Package}/{Target}";
        }

        public string RelativeRpath { get; }

        private bool Parse()
        {
            int nextIndex = 0;
            switch (RawValue[nextIndex])
            {
                case '@':
                    var endIndex = RawValue.IndexOf('/');
                    if (endIndex < 0)
                        return false;
                    Workspace = RawValue[1..endIndex];
                    if (RawValue[endIndex + 1] != '/') return false;
                    nextIndex = endIndex + 2;
                    break;
                case ':':
                    IsRelative = true;
                    Package = "";
                    Workspace = DefaultWorkspace;
                    Target = RawValue[1..];

                    return true;
                case '/':
                    if (RawValue[1] != '/') return false;
                    nextIndex = 2;
                    Workspace = DefaultWorkspace;
                    break;
                default:
                    IsRelative = true;
                    Workspace = DefaultWorkspace;
                    break;
            }

            var defaultTargetIndex = -1;
            for (var i = RawValue.Length - 1; i >= nextIndex; --i)
            {
                if (RawValue[i] == ':')
                {
                    Package = RawValue[nextIndex..i];
                    Target = RawValue[(i + 1)..];
                    return true;
                }

                if (defaultTargetIndex == -1 && RawValue[i] == '/')
                {
                    defaultTargetIndex = i + 1;
                }
            }

            Package = RawValue[nextIndex..];
            Target = RawValue[defaultTargetIndex..];
            return true;
        }

        public string Package { get; set; }

        public string Target { get; set; }

        public bool IsRelative { get; set; }

        public string Workspace { get; set; }

        public bool IsValid { get; }

        public string RawValue { get; }

        public const string DefaultWorkspace = "default";

        public static implicit operator Label(string labelString) => new Label(labelString);
        // public static explicit operator Label(string labelString) => new Label(labelString);
    }
}