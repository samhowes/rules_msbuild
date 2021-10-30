#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NuGetParser
{

    public class CustomException : Exception
    {
        public CustomException(string message) : base(message)
        {
            
        }
    }
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                var argsDict = args.Select(a => a.Split("=")).ToDictionary(p => p[0][2..], p => p[1]);
                
                var context = new NuGetContext(argsDict);
                var restorer = new Restorer(context);
                restorer.Restore();

                var files = new Files();
                var reader = new AssetsReader(files, Path.GetDirectoryName(context.Args["dotnet_path"])!);
                var parser = new Parser(context, files, reader);
                var generator = new BuildGenerator(context);
                parser.Parse();
                generator.GenerateBuildFiles();
                return 0;
            }
            catch (CustomException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            
            return 1;
        }
    }

    public class FrameworkInfo
    {
        public FrameworkInfo(string tfm)
        {
            Tfm = tfm;
        }

        public string Tfm { get; set; }
        public List<FrameworkRestoreGroup> RestoreGroups { get; set; } = new List<FrameworkRestoreGroup>();

        public void AddPackage(string packageName, string packageVersion)
        {
            var id = new PackageId(packageName, packageVersion);
            var found = false;
            foreach (var group in RestoreGroups)
            {
                if (!group.Packages.ContainsKey(id.Name))
                {
                    found = true;
                    group.Packages[id.Name] = id.Version;
                    break;
                }
            }

            if (!found)
            {
                var group = new FrameworkRestoreGroup();
                group.Packages[id.Name] = id.Version;
                RestoreGroups.Add(group);
            }
        }
    }

    public class FrameworkRestoreGroup
    {
        public string ProjectFileName { get; set; } = null!;

        public Dictionary<string, string> Packages { get; set; } =
            new Dictionary<string, string>();

        public string ObjDirectory { get; set; } = null!;
    }


    public class TfmInfo
    {
        public string Tfm { get; }
        public List<Package> ImplicitDeps { get; } = new List<Package>();
        public string? Tfn { get; set; }

        public TfmInfo(string tfm)
        {
            Tfm = tfm;
        }
    }
}