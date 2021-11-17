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
        private readonly string _ideBase;

        public RestoreFixer(BuildContext context, Files files, Paths paths)
        {
            _context = context;
            _target = _context!.Bazel.OutputBase;
            _bazelBase = _context.MSBuild.BaseIntermediateOutputPath;
            _ideBase = Path.GetDirectoryName(_context.MSBuild.BaseIntermediateOutputPath)!;
            _context = context;
            _files = files;
            _paths = paths;
            _escapedTarget = Escape(_target);
        }

        private static string Escape(string s) => s.Replace(@"\", @"\\");
        private static string Unescape(string s) => s.Replace(@"\\", @"\");

        public void Fix(string originalFilePath)
        {
            var rel = Path.GetRelativePath(_bazelBase, originalFilePath);
            var ideFilePath = Path.Combine(_ideBase, rel);
            var bazelFilePath = originalFilePath;

            var contents = _files.GetContents(originalFilePath).AsSpan();
            var isJson = contents[0] == '{';
            var needsEscaping = isJson && Path.DirectorySeparatorChar == '\\';
            var thisTarget = needsEscaping ? _escapedTarget : _target;
            var pathCharLength = needsEscaping ? 2 : 1;

            _files.CreateDirectory(Path.GetDirectoryName(ideFilePath)!);
            using var ideOutput = new StreamWriter(_files.Create(ideFilePath));
            using var bazelOutput = new StreamWriter(_files.Create(bazelFilePath));

            for (;;)
            {
                var thisIndex = contents.IndexOf(thisTarget);
                if (thisIndex == -1) break;

                ideOutput.Write(contents[..thisIndex]);
                bazelOutput.Write(contents[..thisIndex]);

                contents = contents[thisIndex..];

                var endOfPath = contents.IndexOfAny(new[] { '"', ';', '<' });
                var path = contents[..(endOfPath)];
                var idePath = path;

                ideOutput.Write(idePath[..(thisTarget.Length + pathCharLength)]);
                idePath = idePath[(thisTarget.Length + pathCharLength)..];
                if (idePath[.."sandbox".Length].SequenceEqual("sandbox"))
                {
                    idePath = idePath[idePath.IndexOf("execroot")..];
                }

                bool foundDir = false;
                for (var i = idePath.Length - 1; i >= 0; i--)
                {
                    if (idePath[i] != Path.DirectorySeparatorChar) continue;
                    if (foundDir || i < idePath.Length - 1)
                    {
                        if (idePath[i + 1] == '_')
                        {
                            if (idePath[^1] != Path.DirectorySeparatorChar)
                            {
                                i--;
                                if (needsEscaping)
                                    i--;
                            }

                            idePath = idePath[..(i + 1)];
                        }

                        break;
                    }

                    foundDir = true;
                    // double backslash in json?
                    if (needsEscaping)
                        i--;
                }

                ideOutput.Write(idePath);

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