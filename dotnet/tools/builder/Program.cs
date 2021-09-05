using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using System.Threading;
using CommandLine;
using CommandLine.Text;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Locator;
using RulesMSBuild.Tools.Builder.Caching;
using RulesMSBuild.Tools.Builder.Diagnostics;
using RulesMSBuild.Tools.Builder.Diagnostics.GraphViz;
using RulesMSBuild.Tools.Builder.Launcher;
using static RulesMSBuild.Tools.Builder.BazelLogger;

namespace RulesMSBuild.Tools.Builder
{
    public class Program
    {
        static int Main(string[] args)
        {
            if (DebugEnabled)
            {
                Debug($"Received {args.Length} arguments: {string.Join(" ", args)}");
            }

            var code = -1;
            switch (args[0])
            {
                case "inspect":
                    code = Inspect(args[1]);
                    break;
                
                case "launcher":
                    code = MakeLauncher(args.Skip(1));
                    break;

                default:
                    var parser = new CommandLine.Parser(with => with.HelpWriter = null);
            
                    var result = parser.ParseArguments<BuildCommand>(args);
                    if (result.Errors?.Any() == true)
                    {
                        var text = HelpText.AutoBuild(result);
                        Console.Error.WriteLine(text.ToString());
                        return -1;
                    }
                    var command = (BuildCommand)result.Value;

                    switch (command.Action)
                    {
                        case "pack":
                        case "restore":
                        case "publish":
                        case "build":
                            code = Build((BuildCommand) command);
                            break;
                        default:
                            return Fail($"Unknown command: {command.Action}");
                    }

                    break;
            }
            Debug($"exiting with code {code}");
            return code;
        }

        private static int Inspect(string file)
        {
            RegisterSdk("/usr/local/share/dotnet/sdk/5.0.203");
            
            InspectImpl(file);
            return 0;
        }

        private static void InspectImpl(string file)
        {
            var execRootIndex = file.IndexOf("bazel-", StringComparison.OrdinalIgnoreCase);
            var execRoot = file[0..(execRootIndex-1)];
            var outputBase = Path.Combine(execRoot, "bazel-" + Path.GetFileName(execRoot));
            if (Directory.Exists(outputBase))
            {
                execRoot = outputBase;
            }
            else
            {
                outputBase = execRoot;
            }

            var pathMapper = new PathMapper(execRoot, outputBase);
            BuildCache MakeCache()
            {
                var c = new BuildCache(
                    new BazelContext.BazelLabel() {Workspace = "foo", Package = "idk", Name = "bar"},
                    pathMapper,
                    new Files(), null)
                {
                    Manifest = new CacheManifest() {Projects = new Dictionary<string, string>() {[file] = file},}
                };

                return c;
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
                    ("Publish", "lightpink", "lightcoral"),
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
            else if (file.EndsWith(".cache_manifest"))
            {
                Directory.SetCurrentDirectory(execRoot);
                cache.Initialize(file, null);
                var config = cache.ConfigCache.Single(c => c.ProjectFullPath.EndsWith("Tool.csproj"));
                var results = cache.ResultsCache.ResultsDictionary[config.ConfigurationId];
                var computedResults = results.ResultsByTarget["ComputeResolvedFilesToPublishList"];
                var cachedResult = results.ResultsByTarget["_PublishBuildAlternative"];
            }
            
            // var itemGroups = project.Items.GroupBy(i => i.ItemType).OrderBy(g => g.Key).ToList();
        }

        private static int Build(BuildCommand command)
        {
            var context = new BuildContext(command);
            RegisterSdk(context.SdkRoot);
            var builder = new Builder(context, new BuilderDependencies(context));
            return builder.Build();
        }

        private static string? RegisteredSdkRoot;
        private static object _registerLock = new object();
        public static void RegisterSdk(string sdkRoot)
        {
            lock (_registerLock)
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
        }


        private static int MakeLauncher(IEnumerable<string> args)
        {
            var factory = new LauncherFactory();
            return factory.Create(args.ToArray());
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