using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MyRulesDotnet.Tools.Builder
{
    public class ProcessorContext
    {
        // bazel always sends us POSIX paths
        private const char BazelPathChar = '/';
        private readonly bool _normalizePath;


        private string NormalizePath(string input)
        {
            if (!_normalizePath) return input;
            return input.Replace('/', Path.DirectorySeparatorChar);
        }

        // for testing
        public ProcessorContext() { }
        public ProcessorContext(string[] commandArgs, string[] passthroughArgs)
        {
            _normalizePath = Path.DirectorySeparatorChar != BazelPathChar;
            TargetDirectory = NormalizePath(commandArgs[0]);
            OutputDirectory = NormalizePath(commandArgs[1]);
            OutputBase = NormalizePath(commandArgs[2]);
            ChildCommand = passthroughArgs;
            // assumes bazel invokes actions at ExecRoot
            ExecRoot = Directory.GetCurrentDirectory();
        }

        public string TargetDirectory { get; set; }
        public string OutputBase { get; set; }
        public string[] ChildCommand { get; set; }
        public string Suffix { get; set; }
        public string ExecRoot { get; set; }
        public string OutputDirectory { get; set; }
    }

    public class OutputProcessor
    {
        private readonly ProcessorContext _context;

        private const string ExecRoot = "$exec_root$";
        private const string OutputBase = "$output_base$";

        protected virtual void Fail(string message)
        {
            Program.Fail(message);
        }

        public OutputProcessor(ProcessorContext context)
        {
            _context = context;
        }


        /// <summary>
        /// Bazelifies the file contents in a target directory from MsBuild by trimming absolute paths.
        /// </summary>
        public void PostProcess()
        {
            Prepare();

            RunCommand();

            Directory.CreateDirectory(_context.OutputDirectory);

            var regexString = $"(?<output_base>{Regex.Escape(_context.OutputBase)}(?<exec_root>{Regex.Escape(_context.Suffix)})?)";

            // to support files on windows that escape backslashes i.e. json.
            regexString = regexString.Replace(@"\\", @"\\(\\)?");

            var regex = new Regex(regexString,
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Program.Debug($"Using regex: '{regex}'");

            ProcessFiles(_context.TargetDirectory, (info, contents) =>
            {
                var replaced = regex.Replace(contents,
                    (match) => match.Groups["exec_root"].Success ? ExecRoot : OutputBase);
                File.WriteAllText(info.FullName, replaced);

                info.MoveTo(Path.Combine(_context.OutputDirectory, info.Name));
            });
        }

        /// <summary>
        /// Debazelifies the file contents in a directory assuming that the files were bazelified by
        /// <see cref="PostProcess"/>.
        /// </summary>
        public void PreProcess()
        {
            Prepare();

            var regex = new Regex($@"({Regex.Escape(OutputBase)})|({Regex.Escape(ExecRoot)})", RegexOptions.Compiled);
            var escapedOutputBase = _context.OutputBase.Replace(@"\", @"\\");
            var escapedExecRoot = _context.ExecRoot.Replace(@"\", @"\\");
            ProcessFiles(_context.OutputDirectory, (info, contents) =>
            {
                var replaced = regex.Replace(contents,
                    (match) =>
                    {
                        var execRoot = match.Groups[2].Success;
                        return info.Extension switch
                        {
                            ".json" => execRoot ? escapedExecRoot : escapedOutputBase,
                            _ => execRoot ? _context.ExecRoot : _context.OutputBase
                        };
                    });

                File.WriteAllText(Path.Combine(_context.TargetDirectory, info.Name), replaced);
            });

            RunCommand();
        }

        private void ProcessFiles(string directoryPath, Action<FileInfo, string> modifyFile)
        {
            foreach (var info in Directory.EnumerateFiles(directoryPath).Select(f => new FileInfo(f)))
            {
                Program.Debug($"Processing file: {info}");
                // assumes buffering the whole file into memory is okay. This should be a safe assumption. 
                // might be better to make sure we don't open a file we aren't supposed to.
                var contents = File.ReadAllText(info.FullName);
                modifyFile(info, contents);
            }
        }

        private void Prepare()
        {
            if (!_context.TargetDirectory.EndsWith("obj"))
                Fail($"Refusing to process unexpected directory {_context.TargetDirectory}");

            if (Program.DebugEnabled)
            {
                Program.Debug(Directory.GetCurrentDirectory());
                foreach (var entry in Directory.EnumerateDirectories("."))
                    Console.WriteLine(entry);
            }

            if (!_context.ExecRoot.StartsWith(_context.OutputBase))
                Fail($"Refusing to process trim_path {_context.OutputBase} that is not a prefix of" +
                             $" cwd {_context.ExecRoot}");

            _context.Suffix = _context.ExecRoot[_context.OutputBase.Length..];
        }

        protected virtual void RunCommand()
        {
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
            child.StartInfo.UseShellExecute = false;

            // inherit all the streams from this current process
            child.StartInfo.RedirectStandardInput = false;
            child.StartInfo.RedirectStandardOutput = false;
            child.StartInfo.RedirectStandardError = false;

            if (!child.Start())
                Fail("Failed to start child.");
            child.WaitForExit();
        }
    }
}
