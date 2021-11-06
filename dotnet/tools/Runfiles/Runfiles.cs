using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RulesMSBuild.Tools.Bazel
{
    /// <summary>
    /// Runfiles lookup library for Bazel-built Dotnet binaries and tests.
    ///
    /// <list type="number">
    /// <listheader><term>USAGE:</term></listheader>
    /// <item>
    /// <description>Depend on this runfiles library from your build rule:
    /// <code>
    /// dotnet_binary(
    ///     name = "my_binary",
    ///     ...
    ///     deps = ["@rules_msbuild//dotnet/tools/Runfiles"],
    /// )
    /// </code>
    /// </description>
    /// </item>
    /// <item>
    /// Use the runfiles library.
    /// <code>
    /// using RulesMSBuild.Runfiles;
    /// </code>
    /// </item>
    /// <item>
    /// Create a runfiles object and use Rlocation to look up runfile paths.
    /// <code>
    /// public void MyFunction() {
    ///     Runfiles runfiles = Runfiles.Create();
    ///     string path = runfiles.Rlocation("my_workspace/path/to/my/data.txt");
    ///     ...
    /// </code>
    /// </item>
    /// </list>
    /// <para>
    /// If you want to start a subprocess that also needs runfiles, you need to set the right
    /// environment variables for them:
    ///
    /// <code>
    /// var path = r.Rlocation("path/to/binary");
    /// ProcessStartInfo startInfo = new ProcessStartInfo(path);
    /// foreach (var envVar in GetEnvVars())
    /// {
    ///     startInfo.EnvironmentVariables.Add(envVar.Key, envVar.Value);
    /// }
    /// startInfo.UseShellExecute = false;
    /// ...
    /// startInfo.Start();
    /// </code>
    /// </para>
    /// </summary>
    public abstract class Runfiles
    {
        // internal constructor, so only classes in this assembly may extend it.
        internal Runfiles()
        {
        }

        /// <summary>
        /// Bazel always uses forward slash for paths.
        /// </summary>
        public const char PathSeparator = '/';

        /// <summary>
        /// Returns a new <see cref="Runfiles"/> instance.
        /// <para>The returned object is either:</para>
        /// <list type="bullet">
        /// <item>manifest-based, meaning it looks up runfile paths from a manifest file</item>
        /// <item>directory-based, meaning it looks up runfile paths under a given directory path</item>
        /// </list>
        /// 
        /// <para>If <paramref name="env"/> contains a "RUNFILES_MANIFEST_ONLY" with value "1", this method returns a
        /// manifest based implementation. The manifest's path is defined by the "RUNFILES_MANIFEST_FILE" key's value
        /// in <paramref name="env"/></para>
        /// <para>Otherwise this method returns a directory-based implementation. The directory's path is defined by
        /// the value in <paramref name="env"/> under the "RUNFILES_DIR" key.</para>
        /// 
        /// <para>Performance note: the manifest-based implementation eagerly reads and caches the whole manifest file
        /// on instantiation.</para>
        /// </summary>
        /// <param name="env">A dictionary of environment variables</param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static LabelRunfiles Create(IDictionary env)
        {
            const string infoName = "runfiles.info";
            var assemblyLocation = AppDomain.CurrentDomain.BaseDirectory;
            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
            var infoFile = new FileInfo(
                Path.Combine(assemblyDirectory!, infoName));
            if (!infoFile.Exists)
            {
                throw new IOException($"Could not find `{infoName}` file next to {assemblyLocation}");
            }

            var lines = File.ReadAllLines(infoFile.FullName);
            if (lines.Length != 4)
            {
                throw new IOException($"Unexpected `{infoName}` format, expected three lines, got: {lines.Length}");
            }

            var expectedLocation = lines[0];
            var defaultPackage = new Label(lines[1], lines[2]);
            var strategy = lines[3];

            string GetExpectedLocation()
            {
                // expected location is a relative path to <EntryAssembly>.dll.runfiles, as calculated by the builder
                var expectedDir = Path.GetFullPath(Path.Combine(assemblyDirectory, expectedLocation));
                if (!Directory.Exists(expectedDir))
                {
                    throw new IOException($"Failed to find runfiles. No environment variables were set and " +
                                          $"{infoFile.FullName} pointed to {expectedDir} which does not exist.");
                }

                return expectedDir;
            }

            Runfiles runfiles;
            if (strategy == "selfish")
            {
                runfiles = new DirectoryBased(GetExpectedLocation());
            }
            else if (!TryCreate(env, out runfiles))
            {
                // no one has told us where the runfiles are, this means:
                // 1) the user ran a .dll with `dotnet <AssemblyName>.dll`
                //      in this case, the runfiles dir is at <AssemblyName>[.exe].runfiles
                // 2) An ide is executing this assembly, perhaps via debugging
                //      if it's debugging, we most likely can't divine runfiles from Environment.CommandLine because that
                //      will be the debugger arguments

                // since no environment variables were set, we just have to take our best guess at which method to use
                // on non-windows, there *should* be a runfiles tree, so use a directory based.
                if (Path.DirectorySeparatorChar != '\\')
                {
                    runfiles = new DirectoryBased(GetExpectedLocation());
                }
                else
                {
                    // on windows, there *may* be a runfiles tree, but there will always be a manifest, so its safe to 
                    // use that
                    runfiles = new ManifestBased(Path.Combine(GetExpectedLocation(), "MANIFEST"));
                }
            }

            return new LabelRunfiles(runfiles, defaultPackage);
        }


        /// <summary>
        /// Returns a new <see cref="Runfiles"/> instance.
        /// <para>This method passes <see cref="Environment.GetEnvironmentVariables"/> to <see cref="Create"/></para>
        /// </summary>
        public static Runfiles Create()
        {
            return Create(Environment.GetEnvironmentVariables()).Runfiles;
        }

        private static bool TryCreate(IDictionary env, out Runfiles runfiles)
        {
            if (IsManifestOnly(env))
            {
                // On Windows, the launcher sets RUNFILES_MANIFEST_ONLY=1.
                // On every platform, the launcher also sets RUNFILES_MANIFEST_FILE, but on Linux and macOS it's
                // faster to use RUNFILES_DIR.
                {
                    runfiles = new ManifestBased(GetManifestPath(env));
                    return true;
                }
            }

            var value = env["RUNFILES_DIR"] as string;
            if (!string.IsNullOrEmpty(value))
            {
                // bazel, a launcher, or another process has told us where the runfiles are
                runfiles = new DirectoryBased(value);
                return true;
            }

            runfiles = null;
            return false;
        }

        /// <summary>
        /// Returns the runtime path of a runfile (a Bazel-built binary's/test's data-dependency).
        ///
        /// <para>The returned path may not be valid. The caller should check the path's validity and that the
        /// path exists.</para>
        ///
        /// <para>The function may return null. In that case the caller can be sure that the rule does not
        /// know about this data-dependency.</para>
        /// <para>throws ArgumentException if <see cref="path"/> fails validation, for example if it's null or
        ///     empty, or not normalized(contains "./", "../", or "//")</para>
        /// </summary>
        /// <param name="path">runfiles-root-relative path of the runfile, always with forward slashes, regardless of
        /// platform.</param>
        /// <returns></returns>
        public string Rlocation(string path)
        {
            Check.Argument(path != null);
            Check.Argument(!string.IsNullOrEmpty(path));
            Check.Argument(!path.StartsWith("../")
                           && !path.Contains("/..")
                           && !path.StartsWith("./")
                           && !path.Contains("/./")
                           && !path.EndsWith("/.")
                           && !path.Contains("//"),
                $"path is not normalized: \"{path}\"");

            Check.Argument(
                !path.StartsWith("\\"), "path is absolute without a drive letter: \"%s\"", path);
            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return RlocationChecked(path);
        }

        /// <summary>
        /// Returns environment variables for subprocesses.
        /// <para>The caller should add the returned key-value pairs to the environment of subprocesses in
        /// case those subprocesses are also Bazel-built binaries that need to use runfiles.</para>
        /// </summary>
        public abstract Dictionary<string, string> GetEnvVars();

        /// <summary>
        /// Returns true if the platform supports runfiles only via manifests.
        /// </summary>
        private static bool IsManifestOnly(IDictionary env)
        {
            return "1".Equals(env["RUNFILES_MANIFEST_ONLY"]);
        }

        private static string GetManifestPath(IDictionary env)
        {
            var value = env["RUNFILES_MANIFEST_FILE"] as string;

            if (string.IsNullOrEmpty(value))
            {
                throw new IOException(
                    "Cannot load runfiles manifest: $RUNFILES_MANIFEST_ONLY is 1 but"
                    + " $RUNFILES_MANIFEST_FILE is empty or undefined");
            }

            return value;
        }

        public abstract IEnumerable<string> ListRunfiles(string directory = null);
        public abstract string RlocationChecked(string path);

        public static Runfiles CreateManifestBasedForTesting(string manifestPath)
        {
            return new ManifestBased(manifestPath);
        }

        public static Runfiles CreateDirectoryBasedForTesting(string runfilesDir)
        {
            return new DirectoryBased(runfilesDir);
        }

        /// <summary>
        /// <see cref="Runfiles"/> implementation that parses a runfiles-manifest file to look up runfiles.
        /// </summary>
        private sealed class ManifestBased : Runfiles

        {
            private readonly string _manifestPath;
            private readonly IDictionary<string, string> _runfiles;

            public ManifestBased(string manifestPath)
            {
                Check.Argument(!string.IsNullOrEmpty(manifestPath));

                _manifestPath = manifestPath;

                _runfiles = LoadRunfiles(manifestPath);
            }

            private static IDictionary<string, string> LoadRunfiles(string path)
            {
                var result = new Dictionary<string, string>();

                using (var reader =
                    new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read), Encoding.UTF8))
                {
                    while (true)
                    {
                        var line = reader.ReadLine();
                        if (line == null) break;
                        var index = line.IndexOf(' ');
                        var runfile = (index == -1) ? line : line.Substring(0, index);
                        var realPath = (index == -1) ? line : line.Substring(index + 1);
                        result[runfile] = realPath;
                    }
                }

                return new ReadOnlyDictionary<string, string>(result);
            }

            private static string FindRunfilesDir(string manifest)
            {
                if (manifest.EndsWith("/MANIFEST")
                    || manifest.EndsWith("\\MANIFEST")
                    || manifest.EndsWith(".runfiles_manifest"))
                {
                    var path = manifest.Substring(0, manifest.Length - 9);
                    if (Directory.Exists(path))
                    {
                        return path;
                    }
                }

                return null;
            }

            public override string RlocationChecked(string path)
            {
                _runfiles.TryGetValue(path, out var realPath);
                return realPath;
            }

            public override IEnumerable<string> ListRunfiles(string directory)
            {
                return _runfiles.Where(p => p.Key.StartsWith(directory)).Select(p => p.Value);
            }

            public override Dictionary<string, string> GetEnvVars()
            {
                var result = new Dictionary<string, string>(3);
                result["RUNFILES_MANIFEST_ONLY"] = "1";
                result["RUNFILES_MANIFEST_FILE"] = _manifestPath;
                string runfilesDir = FindRunfilesDir(_manifestPath);
                result["RUNFILES_DIR"] = runfilesDir;
                return result;
            }
        }

        /// <summary>
        /// <see cref="Runfiles"/> implementation that appends runfiles paths to the runfiles root.
        /// </summary>
        private sealed class DirectoryBased : Runfiles
        {
            private readonly string _runfilesRoot;

            public DirectoryBased(string runfilesDir)
            {
                Check.Argument(!string.IsNullOrEmpty(runfilesDir));
                Check.Argument(Directory.Exists(runfilesDir));

                _runfilesRoot = runfilesDir;
            }

            public override string RlocationChecked(string path)
            {
                return _runfilesRoot + "/" + path;
            }

            public override IEnumerable<string> ListRunfiles(string directory = null)
            {
                directory = directory != null
                    ? Path.Combine(_runfilesRoot, directory)
                    : _runfilesRoot;

                IEnumerable<string> Walk(string path)
                {
                    foreach (var dir in Directory.EnumerateDirectories(path))
                    foreach (var file in Walk(dir))
                        yield return file;

                    foreach (var file in Directory.EnumerateFiles(path))
                        yield return file;
                }

                return Walk(directory);
            }

            public override Dictionary<string, string> GetEnvVars()
            {
                var result = new Dictionary<string, string>(1) {["RUNFILES_DIR"] = _runfilesRoot};
                return result;
            }
        }

        public void SetEnvVars(IDictionary<string, string> env)
        {
            foreach (var (key, value) in GetEnvVars())
            {
                env[key] = value;
            }
        }
    }
}