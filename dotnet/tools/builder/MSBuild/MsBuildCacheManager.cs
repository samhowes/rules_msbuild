#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Execution;
using RulesMSBuild.Tools.Builder.Diagnostics;
using static RulesMSBuild.Tools.Builder.BazelLogger;

namespace RulesMSBuild.Tools.Builder.MSBuild
{
    public class MsBuildCacheManager
    {
        private readonly string _execRoot;
        private readonly BuildManager _buildManager;
        private readonly BuildManagerFields _fields;

        private static readonly string Root = Path.DirectorySeparatorChar == '\\' ? "C:\\" : "/";

        public MsBuildCacheManager(BuildManager buildManager, string execRoot, TargetGraph? targetGraph)
        {
            _execRoot = execRoot;
            _buildManager = buildManager;
            _fields = new BuildManagerFields();
        }

        public void BeforeExecuteBuild()
        {
            FixPaths(true);
        }

        public void AfterExecuteBuild()
        {
            FixPaths(false);
        }

        private void FixPaths(bool isBeforeBuild)
        {
            var target = isBeforeBuild ? $"{Root}$exec_root" : _execRoot;
            var replacement = isBeforeBuild ? _execRoot : $"{Root}$exec_root";

            FixConfigResults(isBeforeBuild, target, replacement);
            FixBuildResults(isBeforeBuild, target, replacement);
        }

        private void FixConfigResults(bool isBeforeBuild, string target, string replacement)
        {
            var configCache = _fields.GetConfigCache(_buildManager, isBeforeBuild);
            var configurationIdsByMetadata = _fields.GetConfigIdsByMetadata(configCache);
            if (configurationIdsByMetadata!.Count <= 0) return; 
            var configCount = 0;
            var entries = new DictionaryEntry[configurationIdsByMetadata!.Count];
            var i = 0;
#pragma warning disable 8605
            foreach (DictionaryEntry entry in configurationIdsByMetadata)
#pragma warning restore 8605
                entries[i++] = entry;

            var metadataProjectPath = _fields.GetMetadataProjectPath(entries[0].Key);
            
            // we can't modify the dictionary in the foreach above, so do it after the fact
            foreach (var entry in entries)
            {
                var metadata = entry.Key;
                var configId = (int)entry.Value!;
                
                configurationIdsByMetadata.Remove(metadata);
                var path = (string) metadataProjectPath.GetValue(metadata)!;
                var expandedPath = path.Replace(target, replacement);
                metadataProjectPath.SetValue(metadata, expandedPath);
                configurationIdsByMetadata[metadata] = configId;
            }

            foreach (var config in (IEnumerable<object>)configCache)
            {
                var path = _fields.GetConfigProjectPath(config);
                if (path == null) continue;
                if (!path.Contains(target)) continue;
                configCount++;
                Verbose(path);
                _fields.ConfigProjectPath!.SetValue(config, path.Replace(target, replacement));
            }

            Verbose(configCount.ToString());
        }

        private void FixBuildResults(bool isBeforeBuild, string target, string replacement)
        {
            var cache = _fields.GetResultsCache(_buildManager, isBeforeBuild);
            
            if (cache == null) return;

            var targetCount = 0;
            foreach (var buildResult in (IEnumerable<BuildResult>)cache)
            {
                foreach (var (targetName, targetResult) in buildResult.ResultsByTarget)
                {
                    Verbose(targetName);
                    foreach (var item in targetResult.Items)
                    {
                        foreach (var name in item.MetadataNames.Cast<string>())
                        {
                            var meta = item.GetMetadata(name);

                            if (meta.Contains(target))
                            {
                                Verbose($"==> {name}: {meta}");
                                targetCount++;
                                item.SetMetadata(name, meta.Replace(target, replacement));
                            }
                        }


                        if (item.ItemSpec.Contains(target))
                        {
                            Verbose(item.ItemSpec);
                            item.ItemSpec = item.ItemSpec.Replace(target, replacement);
                            targetCount++;
                        }
                    }
                }
            }

            Verbose(targetCount.ToString());
        }
    }
}
