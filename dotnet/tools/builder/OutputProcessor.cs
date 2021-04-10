using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MyRulesDotnet.Tools.Builder
{
    public class ProcessorContext
    {
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

        public OutputProcessor(string[] commandArgs, string[] passthroughArgs)
        {
            Program.Debug(string.Join("; ", commandArgs));
            Program.Debug(string.Join("; ", passthroughArgs));
            _context = new ProcessorContext()
            {
                TargetDirectory = commandArgs[0],
                OutputDirectory = commandArgs[1],
                OutputBase = commandArgs[2],
                ChildCommand = passthroughArgs
            };
        }
        
        /// <summary>
        /// Bazelifies the file contents in a target directory from MsBuild by trimming absolute paths.
        /// </summary>
        public void PostProcess()
        {
            Prepare();

            RunCommand();

            Directory.CreateDirectory(_context.OutputDirectory);

            var regex = new Regex($"({Regex.Escape(_context.OutputBase)}({Regex.Escape(_context.Suffix)})?)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Program.Debug($"Using regex: '{regex}'");
            
            ProcessFiles(_context.TargetDirectory, (info, contents) =>
            {
                var replaced = regex.Replace(contents,
                    (match) => match.Groups[2].Success ? ExecRoot : OutputBase);
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
            ProcessFiles(_context.OutputDirectory, (info, contents) =>
            {
                var replaced = regex.Replace(contents,
                    (match) => match.Groups[1].Success ? _context.OutputBase : _context.ExecRoot);

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
                Program.Fail($"Refusing to process unexpected directory {_context.TargetDirectory}");

            Program.Debug(Directory.GetCurrentDirectory());
            if (Program.DebugEnabled)
                foreach (var entry in Directory.EnumerateDirectories("."))
                    Console.WriteLine(entry);

            // assumes bazel invokes actions at ExecRoot
            _context.ExecRoot = Directory.GetCurrentDirectory();

            if (!_context.ExecRoot.StartsWith(_context.OutputBase))
                Program.Fail($"Refusing to process trim_path {_context.OutputBase} that is not a prefix of" +
                             $" cwd {_context.ExecRoot}");

            _context.Suffix = _context.ExecRoot[_context.OutputBase.Length..];
        }

        private  void RunCommand()
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
                Program.Fail("Failed to start child.");
            child.WaitForExit();
        }
    }
}