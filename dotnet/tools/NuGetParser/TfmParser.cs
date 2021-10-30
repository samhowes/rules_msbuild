using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using static RulesMSBuild.Tools.NuGetParser.BazelLogger;
using static NuGetParser.Package;

namespace NuGetParser
{
    public class Files
    {
        public virtual string GetContents(string path) => File.ReadAllText(path);
        public virtual void WriteContents(string path, string contents) => File.WriteAllText(path, contents);
        public virtual IEnumerable<string> GetFiles(string path) => Directory.EnumerateFiles(path);
        public virtual IEnumerable<string> GetDirectories(string path) => Directory.EnumerateDirectories(path);
        public virtual Stream Create(string path) => File.Create(path);
        public virtual Stream OpenRead(string path) => File.OpenRead(path);

        public virtual bool Exists(string path) => File.Exists(path);

        public virtual IEnumerable<string> EnumerateDirectories(string path)
        {
            return Directory.EnumerateDirectories(path);
        }

        public virtual IEnumerable<string> EnumerateFiles(string path)
        {
            return Directory.EnumerateFiles(path);
        }

        public virtual string[] ReadAllLines(string path)
        {
            return File.ReadAllLines(path);
        }
    }
}