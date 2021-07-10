#nullable enable
using System;
using System.Collections;
using System.Reflection;
using Microsoft.Build.Execution;
using static RulesMSBuild.Tools.Builder.BazelLogger;

namespace RulesMSBuild.Tools.Builder.MSBuild
{
    public class BuildManagerFields
    {
        public static BindingFlags PrivateFlags = BindingFlags.NonPublic | BindingFlags.Instance;
        private FieldInfo? _configurationIdsByMetadata;
        private FieldInfo? _metadataPathField;
        public FieldInfo? ConfigProjectPath;

        public BuildManagerFields()
        {
            ManagerType = typeof(BuildManager);
            // https://github.com/dotnet/msbuild/blob/d07c47adec8d5cf40718ef9a618b0b959cc8be0d/src/Build/BackEnd/BuildManager/BuildManager.cs#L80
            ConfigCache = ManagerType.GetField("_configCache", PrivateFlags)!;
            ResultsCache = ManagerType.GetField("_resultsCache", PrivateFlags)!;
        }
        public FieldInfo ResultsCache { get; set; }
        public FieldInfo ConfigCache { get; }

        public Type ManagerType { get; }

        public object GetConfigCache(BuildManager buildManager, bool isBeforeBuild)
        {
            var cache = ConfigCache!.GetValue(buildManager);

            return GetCacheWithOverride(isBeforeBuild, cache, ConfigCache.Name)!;
        }
        
        public object? GetResultsCache(BuildManager buildManager, bool isBeforeBuild)
        {
            var cache = ResultsCache.GetValue(buildManager);
            return GetCacheWithOverride(isBeforeBuild, cache, ResultsCache.Name);
        }

        private static object? GetCacheWithOverride(bool isBeforeBuild, object? cache, string name)
        {
            var cacheType = cache!.GetType();
            object? underlyingCache;
            // https://github.com/dotnet/msbuild/blob/d07c47adec8d5cf40718ef9a618b0b959cc8be0d/src/Build/BackEnd/Components/Caching/ConfigCacheWithOverride.cs#L13
            if (isBeforeBuild)
            {
                // this is where results from previous cache files are loaded into
                var overrideField =
                    cacheType.GetField("_override", BindingFlags.NonPublic | BindingFlags.Instance);
                underlyingCache = overrideField?.GetValue(cache);
                Debug($"Loaded override cache {underlyingCache}");
            }
            else
            {
                // this is where the results from the current build are stored
                var field =
                    cacheType.GetProperty("CurrentCache", BindingFlags.Public | BindingFlags.Instance);
                if (field == null)
                {
                    // this is the case when we didn't use any input caches, i.e. we are building a leaf project
                    underlyingCache = cache;
                }
                else
                {
                    underlyingCache = field!.GetValue(cache);
                }
            }

            Verify(underlyingCache, name);
            return underlyingCache;
        }

        private static void Verify(object? requiredField, string name)
        {
            if (requiredField == null)
            {
                throw new Exception(
                    $"Failed to read build manager field {name}, please file an issue.");
            }
        }
        
        public IDictionary GetConfigIdsByMetadata(object configCache)
        {
            // https://github.com/dotnet/msbuild/blob/d07c47adec8d5cf40718ef9a618b0b959cc8be0d/src/Build/BackEnd/Components/Caching/ConfigCache.cs#L28
            _configurationIdsByMetadata ??= configCache.GetType().GetField("_configurationIdsByMetadata", PrivateFlags);
            Verify(_configurationIdsByMetadata, nameof(_configurationIdsByMetadata));
            var configurationIdsByMetadata = (IDictionary)_configurationIdsByMetadata!.GetValue(configCache)!;
            return configurationIdsByMetadata!;
        }

        public FieldInfo GetMetadataProjectPath(object metadata)
        {
            // https://github.com/dotnet/msbuild/blob/d07c47adec8d5cf40718ef9a618b0b959cc8be0d/src/Build/BackEnd/Shared/ConfigurationMetadata.cs#L65
            _metadataPathField ??= metadata.GetType().GetField("_projectFullPath", PrivateFlags);
            return _metadataPathField!;
        }

        public string? GetConfigProjectPath(object config)
        {
            // https://github.com/dotnet/msbuild/blob/d07c47adec8d5cf40718ef9a618b0b959cc8be0d/src/Build/BackEnd/Shared/BuildRequestConfiguration.cs#L45
            ConfigProjectPath ??= config.GetType().GetField("_projectFullPath", PrivateFlags);
            var path = ConfigProjectPath!.GetValue(config);
            return path as string;
        }
    }
}