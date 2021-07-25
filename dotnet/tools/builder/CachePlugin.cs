using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Experimental.ProjectCache;
using RulesMSBuild.Tools.Builder.Caching;
using RulesMSBuild.Tools.Builder.Diagnostics;
using static RulesMSBuild.Tools.Builder.BazelLogger;

namespace RulesMSBuild.Tools.Builder
{
    public class CachePlugin : ProjectCachePluginBase
    {
        private readonly TargetGraph? _targetGraph;
        private readonly BuildCache _buildCache;

        public CachePlugin(TargetGraph? targetGraph, BuildCache buildCache)
        {
            _targetGraph = targetGraph;
            _buildCache = buildCache;
        }

        public override Task BeginBuildAsync(CacheContext context, PluginLoggerBase logger,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override async Task<CacheResult> GetCacheResultAsync(BuildRequestData buildRequest, PluginLoggerBase logger,
            CancellationToken cancellationToken)
        {
            // var resultTask = _buildCache.TryGetResults(buildRequest.ProjectFullPath);
            // if (resultTask == null)
            // {
            //     return CacheResult.IndicateNonCacheHit(CacheResultType.CacheMiss);
            // };
            // Debugger.WaitForAttach();
            // var result = await resultTask;

            // var targetResults = new List<TargetResult>();
            // foreach (var targetName in buildRequest.TargetNames)
            // {
            //     if (result.ResultsByTarget.TryGetValue(targetName, out var targetResult))
            //     {
            //         targetResults.Add(targetResult);
            //         Debug($"Target result hit: {targetName}");
            //     }
            //     else
            //     {
            //         Debug($"Target result miss: {targetName}");
            //     } 
            // }
            
            return CacheResult.IndicateNonCacheHit(CacheResultType.CacheMiss);
            
            // return Task.FromResult(CacheResult.IndicateNonCacheHit(CacheResultType.CacheMiss));
        }

        public override Task EndBuildAsync(PluginLoggerBase logger, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
    
    public class GlobalPropertyIgnoringConfigCache : ConfigCacheWithOverride
    {
        public GlobalPropertyIgnoringConfigCache(IConfigCache @override) : base(@override)
        {
        }
    }
}