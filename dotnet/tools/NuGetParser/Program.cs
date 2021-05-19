#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace NuGetParser
{
    class Program
    {
        static int Main(string[] args)
        {
            string intermediateBase = "";
            string packagesFolder = "";
            List<string>? projects = null;
            var dict = new Dictionary<string, string>();
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg[0] == '-')
                {
                    var name = arg[1..];
                    switch (name.ToLower())
                    {
                        case "dotnet_path":
                            break;
                        case "intermediate_base":
                            intermediateBase = args[i + 1];
                            break;
                        case "packages_folder":
                            packagesFolder = args[i + 1];
                            break;
                        default:
                            dict[name] = args[i + 1];
                            break;
                    }

                    i++;
                    continue;
                }

                projects = args[i..].ToList();
                break;
            }
            Console.WriteLine(string.Join(",", projects!));

            var parser = new Parser(intermediateBase, packagesFolder, dict);
            
            if (!parser.Parse(projects)) return 1;
            return 0;
        }
    }

    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
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