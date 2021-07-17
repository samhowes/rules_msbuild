using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Locator;
using RulesMSBuild.Tools.Builder.Diagnostics;
using RulesMSBuild.Tools.Builder.Diagnostics.GraphViz;
using RulesMSBuild.Tools.Builder.Launcher;
using static RulesMSBuild.Tools.Builder.BazelLogger;

namespace RulesMSBuild.Tools.Builder
{
    public class Command
    {
        public string Action = null!;
        public readonly List<string> PositionalArgs = new List<string>();
        public readonly Dictionary<string, string> NamedArgs = new Dictionary<string, string>();
    }

    public class Program
    {
        private static Regex MsBuildVariableRegex = new Regex(@"\$\((\w+)\)", RegexOptions.Compiled);
        
        static int Main(string[] args)
        {
            // Environment.SetEnvironmentVariable("MSBUILDDONOTTHROWINTERNAL", "1");
            if (DebugEnabled)
            {
                Debug($"Received {args.Length} arguments: {string.Join(" ", args)}");
            }
            
            var command = ParseArgs(args);

            switch (command.Action)
            {
                case "inspect":
                    return Inspect(command);
                
                case "launcher":
                    return MakeLauncher(command);
                
                case "pack":
                case "restore":
                case "publish":
                case "build":
                    return Build(command);
                    
                default:
                    return Fail($"Unknown command: {command.Action}");
                    
            }
        }

        private static int Inspect(Command command)
        {
            RegisterSdk("/usr/local/share/dotnet/sdk/5.0.203");
            var file = command.PositionalArgs[0];
            InspectImpl(file);
            return 0;
        }

        private static void InspectImpl(string file)
        {
            var execRootIndex = file.IndexOf("bazel-", StringComparison.OrdinalIgnoreCase);
            var execRoot = file[0..(execRootIndex-1)];
            var outputBase = execRoot;

            var pathMapper = new PathMapper(execRoot, outputBase);
            BuildCache MakeCache()
            {
                return new BuildCache(new CacheManifest()
                {
                    Projects = new Dictionary<string, string>(){[file] = file},
                    Results = new Dictionary<string, string>()
                }, pathMapper, new Files());
            }

            var cache = MakeCache();
            ProjectInstance? project;
            if (file.EndsWith(".csproj"))
            {
                var targetGraph = new TargetGraph(outputBase, file, null);
                var loader = new ProjectLoader(file, cache, pathMapper, targetGraph);
                var projectCollection = new ProjectCollection(new Dictionary<string, string>()
                {
                    ["RestoreUseStaticGraphEvaluation"] = "true",
                    ["NoBuild"] = "true",
                });
                project = loader.Load(projectCollection);
                var prop = project.Properties.FirstOrDefault(p => p.Name == "RestoreUseStaticGraphEvaluation");
                var path = Path.GetTempFileName();
                cache.Project = project;
                cache.SaveProject(path);
                cache = MakeCache();
                project = cache.LoadProjectImpl(path);
                
                var cachePoints = new HashSet<TargetGraph.Node>();
                var cluster = targetGraph.GetOrAddCluster(pathMapper.ToBazel(file));
                foreach (var (targetName, color, darker) in new []
                {
                    ("Restore", "skyblue1", "#1f78b4"),
                    ("Build", "lightgreen", "#33a02c"),
                    ("PublishOnly", "lightpink", "#e31a1c"),
                    ("Pack", "sandybrown", "#ff7f00")
                })
                {
                    void Color(TargetGraph.Node node)
                    {
                        node.Color = node.ReferencedExternally ? darker : color;
                        foreach (var edge in node.Dependencies.Values.Cast<TargetGraph.Edge>())
                        {
                            var to = edge.To;
                            if (to.Color != null)
                            {
                                if (to.Color != color)
                                {
                                    edge.ShouldCache = true;
                                    to.CachePoint = true;
                                    cachePoints.Add(to);    
                                }
                                
                                continue;
                            }
                            Color(edge.To);
                        }
                    }

                    var entry = cluster.Nodes[targetName];
                    
                    Color(entry);
                }

                var dot = targetGraph.ToDot(DotWriter.StyleMode.Inspect);
                var dotPath = file.Replace("bazel-rules_msbuild/", "") + ".dot";
                File.WriteAllText(dotPath, dot);
                var svgPath = dotPath + ".svg";
                var args = $"-Tsvg -o {svgPath} {dotPath}";
                Console.WriteLine($"dot {args}");
                Process.Start("dot", args).WaitForExit();
                Process.Start($"open", $"-a \"Google Chrome\" {svgPath}");

            }
            else if (file.Contains("csproj"))
            {
                project = cache.LoadProjectImpl(file);    
            }
            else
            {
                var results = cache.LoadResults(file);
            }
            
            // var itemGroups = project.Items.GroupBy(i => i.ItemType).OrderBy(g => g.Key).ToList();
        }

        private static Command ParseArgs(string[] args)
        {
            var command = new Command {Action = args[0]};
            ParseArgsImpl(args, 1, command);
            return command;
        }

        private static void ParseArgsImpl(string[] args, int start, Command command)
        {
            for (var i = start; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg == "@file")
                {
                    var fileArgs = File.ReadAllLines(args[i + 1])
                        .SelectMany(l => l.Split(' '))
                        .ToArray();
                    ParseArgsImpl(fileArgs, 0, command);
                    i++;
                    continue;
                }
                
                if (arg.Length == 0 || arg[0] != '-')
                {
                    command.PositionalArgs.Add(arg);
                    continue;
                }

                // assume a well formed array of args in the form [`--name` `value`]
                var name = arg[2..];
                var value = args[i + 1];
                command.NamedArgs[name] = value;
                i++;
            }
        }

        private static int Build(Command command)
        {
            var context = new BuildContext(command);
            RegisterSdk(context.SdkRoot);
            var builder = new Builder(context);
            return builder.Build();
        }

        private static string? RegisteredSdkRoot;
        public static void RegisterSdk(string sdkRoot)
        {
            if (RegisteredSdkRoot != null)
            {
                if (RegisteredSdkRoot != sdkRoot)
                    throw new Exception($"SdkRoot {RegisteredSdkRoot} is already registered and is different than {sdkRoot}.");
                return;
            }

            RegisteredSdkRoot = sdkRoot;
            
            CustomAssemblyLoader.Register();
            var dotNetSdkPath = sdkRoot.EndsWith('/') ? sdkRoot : sdkRoot + Path.DirectorySeparatorChar;
            foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
            {
                ["MSBUILD_EXE_PATH"] = dotNetSdkPath + "MSBuild.dll",
                ["MSBuildExtensionsPath"] = dotNetSdkPath,
                ["MSBuildSDKsPath"] = dotNetSdkPath + "Sdks"
            })
                Environment.SetEnvironmentVariable(keyValuePair.Key, keyValuePair.Value);

            MSBuildLocator.RegisterMSBuildPath(sdkRoot);
        }


        private static int MakeLauncher(Command command)
        {
            var factory = new LauncherFactory();
            return factory.Create(command.PositionalArgs.ToArray());
        }
    }

    public static class CustomAssemblyLoader
    {
        private static readonly Dictionary<string, Assembly> LoadedAssemblies = new ();
        private static readonly string SearchDirectory = Path.GetDirectoryName(typeof(CustomAssemblyLoader).Assembly.Location)!;

        public static void Register()
        {
            AssemblyLoadContext.Default.Resolving += TryLoadAssembly;
        }

        private static Assembly? TryLoadAssembly(AssemblyLoadContext arg1, AssemblyName assemblyName)
        {
            Dictionary<string, Assembly> dictionary = LoadedAssemblies;
            bool lockTaken = false;
            try
            {
                Monitor.Enter((object) dictionary, ref lockTaken);
                Assembly? assembly;
                if (LoadedAssemblies.TryGetValue(assemblyName.FullName, out assembly))
                    return assembly;
                
                string str = Path.Combine(SearchDirectory, assemblyName.Name + ".dll");
                if (File.Exists(str))
                {
                    assembly = Assembly.LoadFrom(str);
                    LoadedAssemblies.Add(assemblyName.FullName, assembly);
                    return assembly;
                }
            
                return null;
            }
            finally
            {
                if (lockTaken)
                    Monitor.Exit((object) dictionary);
            }
        }
    }
}