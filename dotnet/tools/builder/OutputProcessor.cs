using System;
using System.IO;
using System.Text.RegularExpressions;
using static RulesMSBuild.Tools.Builder.BazelLogger;

namespace RulesMSBuild.Tools.Builder
{
    public class OutputProcessor
    {
        private readonly BuildContext _context;
        private Regex _outputFileRegex;

        private const string ExecRoot = "/$exec_root$";
        private const string OutputBase = "/$output_base$";

        protected virtual void Fail(string message)
        { 
            BazelLogger.Fail(message);
        }

        public OutputProcessor(BuildContext context)
        {
            _context = context;
            
            var regexString =
                $"(?<output_base>{Regex.Escape(_context.Bazel.OutputBase)}(?<exec_root>{Regex.Escape("")})?)";

            // to support files on windows that escape backslashes i.e. json.
            regexString = regexString.Replace(@"\\", @"\\(\\)?");

            _outputFileRegex = new Regex(regexString, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Debug($"Using regex: '{_outputFileRegex}'");
        }


        /// <summary>
        /// Bazelifies the file contents in a target directory from MsBuild by trimming absolute paths.
        /// </summary>
        public int PostProcess()
        {
            var exitCode = RunCommand();
            return exitCode;
        }

        private void ProcessOutputFile(string contents, FileInfo info, string destinationDirectory)
        {
            var replaced = _outputFileRegex.Replace(contents,
                (match) =>
                {
                    if (match.Groups["exec_root"].Success)
                    {
                        // Console.WriteLine(contents[match.Index..(match.Index + match.Value.Length + 100)]);
                        return ExecRoot;
                    }
                    else
                    {
                        return OutputBase;
                    }
                });
            File.WriteAllText(info.FullName, replaced);

            info.MoveTo(Path.Combine(destinationDirectory, info.Name), true);
        }

        /// <summary>
        /// Debazelifies the file contents in a directory assuming that the files were bazelified by
        /// <see cref="PostProcess"/>.
        /// </summary>
        public int PreProcess()
        {
            var intermediatePath = Path.Combine(_context.MSBuild.BaseIntermediateOutputPath, _context.Tfm);
            var processedPath = Path.Combine(intermediatePath, "processed");

            var directory = new DirectoryInfo(processedPath);
            if (_context.Command.Action == "publish" && directory.Exists)
            {
                foreach (var file in directory.GetFiles())
                {
                    // prevent the warning:
                    // Unable to use package assets cache due to I/O error. This can occur when the same project is
                    // built more than once in parallel. Performance may be degraded, but the build result will not be
                    // impacted.

                    var copied = file.CopyTo(Path.Combine(intermediatePath, file.Name), true);
                    copied.IsReadOnly = false;
                }
            }


            var regex = new Regex($@"({Regex.Escape(OutputBase)})|({Regex.Escape(ExecRoot)})", RegexOptions.Compiled);
            var escapedOutputBase = _context.Bazel.OutputBase.Replace(@"\", @"\\");
            var escapedExecRoot = _context.Bazel.ExecRoot.Replace(@"\", @"\\");
            ProcessFiles(Path.Combine(_context.MSBuild.BaseIntermediateOutputPath, "processed"), (info, contents) =>
            {
                var replaced = regex.Replace(contents,
                    (match) =>
                    {
                        var execRoot = match.Groups[2].Success;
                        return info.Extension switch
                        {
                            ".json" => execRoot ? escapedExecRoot : escapedOutputBase,
                            _ => execRoot ? _context.Bazel.ExecRoot : _context.Bazel.OutputBase
                        };
                    });

                File.WriteAllText(Path.Combine(_context.MSBuild.BaseIntermediateOutputPath, info.Name), replaced);
            });

            var exitCode = RunCommand();
            if (exitCode != 0) return exitCode;

            if (_context.Command.Action == "build")
            {
                var intermediateDirectory = new DirectoryInfo(intermediatePath);

                var processed = intermediateDirectory.CreateSubdirectory("processed");
                foreach (var file in intermediateDirectory.GetFiles("*.cache"))
                {
                    // msbuild opens this file for writing in Publish, which causes a warning to display because bazel 
                    // makes it readonly. We'll copy it back into place later.
                    file.MoveTo(Path.Combine(processed.FullName, file.Name));
                }

                var resultCache = _context.ProjectFile + ".cache";
                var outputDir = Path.Combine(Path.GetDirectoryName(resultCache)!, "processed");
                Directory.CreateDirectory(outputDir);
                var info = new FileInfo(resultCache);
                info.MoveTo(Path.Combine(outputDir, info.Name));
                // ProcessOutputFile(File.ReadAllText(resultCache), info, outputDir);
                
            }

            return 0;
        }

        private void ProcessFiles(string directoryPath, Action<FileInfo, string> modifyFile)
        {
            foreach (var info in new DirectoryInfo(directoryPath).GetFiles())
            {
                Debug($"Processing file: {info}");
                // assumes buffering the whole file into memory is okay. This should be a safe assumption. 
                // might be better to make sure we don't open a file we aren't supposed to.
                try
                {
                    var contents = File.ReadAllText(info.FullName);
                    modifyFile(info, contents);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to process file: {info.FullName} see inner exception for details", ex);
                }
            }
        }
        
        protected virtual int RunCommand()
        {
            var builder = new Builder(_context, null!);
            return builder.Build();
        }
    }
}