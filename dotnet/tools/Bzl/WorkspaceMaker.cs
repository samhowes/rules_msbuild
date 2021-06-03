using System.IO;
using NuGetParser;

namespace Bzl
{
    public class WorkspaceMaker
    {
        private readonly string _workspaceRoot;
        private readonly string _workspaceName;

        public WorkspaceMaker(string workspaceRoot, string workspaceName)
        {
            _workspaceRoot = workspaceRoot;
            _workspaceName = workspaceName;
        }

        public void Init()
        {
            var workspaceFile = new FileInfo(Path.Combine(_workspaceRoot, "WORKSPACE"));
            if (!workspaceFile.Exists)
            {
                using var f = new BuildWriter(workspaceFile.Create());
                f.Call("workspace", ("name", _workspaceName));
            }

            var msbuildRoot = _workspaceRoot;
            Files.Walk(_workspaceRoot, (path, isDirectory) =>
            {
                if (isDirectory) return true;
                if (path.EndsWith(".sln") || path.EndsWith("proj"))
                {
                    msbuildRoot = Path.GetDirectoryName(path);
                    return false;
                }

                return true;
            });

            foreach (var file in new []
            {
                "Directory.Build.props",
                "Directory.Build.targets",
                "Directory.Solution.props",
                "Directory.Solution.targets",
                "Bazel.props",
            })
            {
                using var _ = File.Create(Path.Combine(msbuildRoot, file));
            }
        }
    }
}