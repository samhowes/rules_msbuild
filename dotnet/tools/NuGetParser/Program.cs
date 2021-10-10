#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetParser
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                var argsDict = args.Select(a => a.Split("=")).ToDictionary(p => p[0][2..], p => p[1]);
                var restorer = new Restorer(argsDict["spec_path"], argsDict["dotnet_path"], argsDict["test_logger"]);
                var frameworks = restorer.Restore();

                var files = new Files();
                var parser = new Parser(argsDict["packages_folder"], files, Console.WriteLine, 
                    new AssetsReader(files));

                if (!parser.Parse(frameworks, argsDict)) return 1;
                return 0;
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