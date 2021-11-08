using System;
using System.IO;
using System.Linq;

namespace RulesMSBuild.Tools.Builder
{
    public class RestoreFixer
    {
        private readonly string _target;
        private readonly string _bazelBase;
        private readonly BuildContext _context;
        private readonly Files _files;
        private readonly Paths _paths;
        private readonly string _escapedTarget;

        public RestoreFixer(BuildContext context, Files files, Paths paths)
        {
            _context = context;
            _target = _context!.Bazel.OutputBase;
            _bazelBase = Path.Combine(_context.MSBuild.BaseIntermediateOutputPath, "_");
            _context = context;
            _files = files;
            _paths = paths;
            _escapedTarget = Escape(_target);
        }

        private static string Escape(string s) => s.Replace(@"\", @"\\");
        private static string Unescape(string s) => s.Replace(@"\\", @"\");

        public void Fix(string ideFileName)
        {
            var bazelFileName = _paths.Combine(_bazelBase, Path.GetFileName(ideFileName));

            var contents = _files.GetContents(ideFileName); //.AsSpan();
            var isJson = contents[0] == '{';
            var needsEscaping = isJson && Path.DirectorySeparatorChar == '\\';
            var thisTarget = needsEscaping ? _escapedTarget : _target;
            var pathCharLength = needsEscaping ? 2 : 1;

            using var ideOutput = new StreamWriter(_files.Create(ideFileName));
            using var bazelOutput = new StreamWriter(_files.Create(bazelFileName));

            for (;;)
            {
                var thisIndex = contents.IndexOf(thisTarget);
                if (thisIndex == -1) break;

                ideOutput.Write(contents[..thisIndex]);
                bazelOutput.Write(contents[..thisIndex]);

                contents = contents[thisIndex..];

                var endOfPath = contents.IndexOfAny(new[] {'"', ';', '<'});
                var sandboxPath = contents[(thisTarget.Length + pathCharLength)..endOfPath];
                var path = contents[..(endOfPath)];
                // next segment will be "sandbox" or "external"
                if (sandboxPath[.."sandbox".Length].SequenceEqual("sandbox"))
                {
                    // execroot path
                    var execIndex = sandboxPath.IndexOf("execroot");
                    ideOutput.Write(contents[..(thisTarget.Length + pathCharLength)]);
                    ideOutput.Write(sandboxPath[execIndex..]);
                }
                else
                {
                    // output_base path
                    ideOutput.Write(path);
                }


                var pathString = path.ToString();
                if (needsEscaping)
                    pathString = Unescape(pathString);

                pathString = isJson
                    ? _paths.GetRelativePath(_context.ProjectDirectory, pathString)
                    : _paths.Combine("$(ExecRoot)", _paths.GetRelativePath(_context.Bazel.ExecRoot, pathString));

                if (needsEscaping)
                    pathString = Escape(pathString);

                bazelOutput.Write(pathString);
                contents = contents[endOfPath..];
            }

            bazelOutput.Write(contents);
            ideOutput.Write(contents);

            bazelOutput.Flush();
            ideOutput.Flush();
        }
    }
}