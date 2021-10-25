namespace RulesMSBuild.Tools.Bazel
{
    public class Label
    {
        public Label(string workspace, string package)
        {
            Workspace = workspace;
            Package = package;
        }
        public Label(string rawValue)
        {
            RawValue = rawValue;
            IsValid = Parse();
            if (!IsValid) return;

            if (Package == "")
            {
                RelativeRpath = Name;
            }
            else if (Name == "")
            {
                RelativeRpath = Package;
            }
            else
            {
                RelativeRpath = $"{Package}/{Name}";
            }
            
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
                    Workspace = RawValue[1..endIndex++];
                    if (RawValue[endIndex++] != '/') return false;
                    nextIndex = endIndex;
                    break;
                case ':':
                    IsRelative = true;
                    Package = "";
                    Workspace = DefaultWorkspace;
                    Name = RawValue[1..];

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

            var lastSlash = (int?)null;
            for (var i = RawValue.Length - 1; i >= nextIndex; --i)
            {
                switch (RawValue[i])
                {
                    case ':':
                        Package = RawValue[nextIndex..i];
                        Name = RawValue[(i + 1)..];
                        return true;
                    case '/':
                        lastSlash ??= i;
                        break;
                }
            }

            Package = RawValue[nextIndex..];
            Name = RawValue[(lastSlash!.Value+1)..];
            return true;
        }

        public string Package { get; set; }

        public string Name { get; set; }

        public bool IsRelative { get; set; }

        public string Workspace { get; set; }

        public bool IsValid { get; }

        public string RawValue { get; }

        public const string DefaultWorkspace = "default";

        public static implicit operator Label(string labelString) => new Label(labelString);
        // public static explicit operator Label(string labelString) => new Label(labelString);
    }
}
