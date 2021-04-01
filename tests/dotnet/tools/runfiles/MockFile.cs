using System;
using System.IO;
using System.Text;

namespace MyRulesDotnet.Tools.RunfilesTests
{
    public class MockFile : IDisposable
    {
        public string Path;

        public MockFile(params string[] lines)
        {
            Path = Files.CreateTempFile();
            File.WriteAllLines(Path, lines, Encoding.UTF8);
        }

        public void Dispose()
        {
            if (Path != null) File.Delete(Path);
        }
    }
}
