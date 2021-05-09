using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace MyRulesDotnet.Tools.Builder
{
    public class OutputProcessor
    {
        private readonly ProcessorContext _context;
        private Regex _outputFileRegex;
        private const string ContentKey = "content";
        private const string RunfilesKey = "runfiles";
        private const string RunfilesDirectoryKey = "runfiles_directory";

        private const string ExecRoot = "$exec_root$";
        private const string OutputBase = "$output_base$";

        protected virtual void Fail(string message)
        {
            Program.Fail(message);
        }

        public OutputProcessor(ProcessorContext context)
        {
            _context = context;
            
            var regexString =
                $"(?<output_base>{Regex.Escape(_context.BazelOutputBase)}(?<exec_root>{Regex.Escape(_context.Suffix)})?)";

            // to support files on windows that escape backslashes i.e. json.
            regexString = regexString.Replace(@"\\", @"\\(\\)?");

            _outputFileRegex = new Regex(regexString, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Program.Debug($"Using regex: '{_outputFileRegex}'");
        }


        /// <summary>
        /// Bazelifies the file contents in a target directory from MsBuild by trimming absolute paths.
        /// </summary>
        public int PostProcess()
        {
            var exitCode = RunCommand();
            if (exitCode != 0) return exitCode;

            var outputDir = Path.Combine(_context.IntermediateBase, "processed");
            Directory.CreateDirectory(outputDir);

            ProcessFiles(_context.IntermediateBase, (info, contents) => { ProcessOutputFile(contents, info, outputDir); });

            return 0;
        }

        private void ProcessOutputFile(string contents, FileInfo info, string destinationDirectory)
        {
            var replaced = _outputFileRegex.Replace(contents,
                (match) => match.Groups["exec_root"].Success ? ExecRoot : OutputBase);
            File.WriteAllText(info.FullName, replaced);

            info.MoveTo(Path.Combine(destinationDirectory, info.Name), true);
        }

        /// <summary>
        /// Debazelifies the file contents in a directory assuming that the files were bazelified by
        /// <see cref="PostProcess"/>.
        /// </summary>
        public int PreProcess()
        {
            var intermediatePath = Path.Combine(_context.IntermediateBase, _context.Tfm);
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
            var escapedOutputBase = _context.BazelOutputBase.Replace(@"\", @"\\");
            var escapedExecRoot = _context.ExecRoot.Replace(@"\", @"\\");
            ProcessFiles(Path.Combine(_context.IntermediateBase, "processed"), (info, contents) =>
            {
                var replaced = regex.Replace(contents,
                    (match) =>
                    {
                        var execRoot = match.Groups[2].Success;
                        return info.Extension switch
                        {
                            ".json" => execRoot ? escapedExecRoot : escapedOutputBase,
                            _ => execRoot ? _context.ExecRoot : _context.BazelOutputBase
                        };
                    });

                File.WriteAllText(Path.Combine(_context.IntermediateBase, info.Name), replaced);
            });

            var exitCode = RunCommand();
            if (exitCode != 0) return exitCode;

            CopyFiles(ContentKey, _context.OutputDirectory, true);
            if (_context.Command.NamedArgs.TryGetValue(RunfilesDirectoryKey, out var runfilesDirectory))
            {
                CopyFiles(RunfilesKey, Path.Combine(_context.OutputDirectory, runfilesDirectory));
            }

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
                ProcessOutputFile(File.ReadAllText(resultCache), info, outputDir);
                
            }

            return 0;
        }

        private void CopyFiles(string filesKey, string destinationDirectory, bool trimPackage = false)
        {
            if (!_context.Command.NamedArgs.TryGetValue(filesKey, out var contentListString) ||
                contentListString == "") return;
            var contentList = contentListString.Split(";");
            var createdDirectories = new HashSet<string>();
            foreach (var filePath in contentList)
            {
                var src = new FileInfo(filePath);
                string destinationPath;
                if (filePath.StartsWith("external/"))
                {
                    destinationPath = filePath.Substring("external/".Length);
                }
                else if (trimPackage && filePath.StartsWith(_context.Package))
                {
                    destinationPath = filePath.Substring(_context.Package.Length + 1);
                }
                else
                {
                    destinationPath = Path.Combine(_context.Workspace, filePath);
                }

                var dest = new FileInfo(Path.Combine(destinationDirectory, destinationPath));

                if (!dest.Exists || src.LastWriteTime > dest.LastWriteTime)
                {
                    if (!createdDirectories.Contains(dest.DirectoryName))
                    {
                        Directory.CreateDirectory(dest.DirectoryName);
                        createdDirectories.Add(dest.DirectoryName);
                    }

                    src.CopyTo(dest.FullName, true);
                }
            }
        }

        private void ProcessFiles(string directoryPath, Action<FileInfo, string> modifyFile)
        {
            foreach (var info in new DirectoryInfo(directoryPath).GetFiles())
            {
                Program.Debug($"Processing file: {info}");
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
            // var sandboxBase = Path.GetDirectoryName(Path.GetDirectoryName(_context.ExecRoot));
            //
            // var document = new XDocument(
            //     new XElement("Project",
            //         new XElement("PropertyGroup", 
            //             new XElement("PathMap", $"{_context.BazelOutputBase}={sandboxBase}"),
            //             // new XElement("PathMap", $"{sandboxBase}={ _context.BazelOutputBase}"),
            //             new XElement("ContinuousIntegrationBuild", $"true")),
            //         
            //         new XElement("ItemGroup",
            //             new XElement("SourceRoot",
            //                 new XAttribute("Include", "$(MSBuildStartupDirectory)/"))))
            //     );
            //
            // var outputPath = Path.Combine(Path.GetDirectoryName(_context.ProjectFile), "Directory.build.props");
            // using (var writer = new XmlTextWriter(outputPath, Encoding.UTF8))
            // {
            //     writer.Formatting = Formatting.Indented;
            //     document.WriteTo(writer);
            // }
            
            using var child = new Process
            {
                StartInfo =
                {
                    FileName = _context.ChildCommand[0]
                }
            };
            Program.Debug("Executing: " + string.Join(" ", _context.ChildCommand));
            foreach (var arg in _context.ChildCommand[1..])
                child.StartInfo.ArgumentList.Add(arg);
            
            child.StartInfo.ArgumentList.Add("-outputResultsCache:" + _context.ProjectFile + ".cache");
            child.StartInfo.UseShellExecute = false;

            // inherit all the streams from this current process
            child.StartInfo.RedirectStandardInput = false;
            child.StartInfo.RedirectStandardOutput = false;
            child.StartInfo.RedirectStandardError = false;

            if (!child.Start())
                Fail("Failed to start child.");
            child.WaitForExit();
            return child.ExitCode;
        }
    }
}