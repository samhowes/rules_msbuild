using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace RulesMSBuild.Tools.Builder.Caching
{
    // public class BazelBuildResult : BuildResult
    // {
    //     public BazelBuildResult(BuildResult result, int submissionId, int configurationId, int requestId, int parentRequestId, int nodeRequestId)
    //         : base(result, submissionId, configurationId, requestId, parentRequestId, nodeRequestId)
    //     {
    //     }
    //
    //     public HashSet<KeyValuePair<string, TargetResult>> NewResults { get; } = new HashSet<KeyValuePair<string, TargetResult>>();
    //
    //     public override void MergeResults(BuildResult results)
    //     {
    //         ErrorUtilities.VerifyThrowArgumentNull(results, nameof(results));
    //         ErrorUtilities.VerifyThrow(results.ConfigurationId == ConfigurationId, "Result configurations don't match");
    //
    //         // If we are merging with ourself or with a shallow clone, do nothing.
    //         if (ReferenceEquals(this, results) || ReferenceEquals(_resultsByTarget, results._resultsByTarget))
    //         {
    //             return;
    //         }
    //
    //         // Merge in the results
    //         foreach (KeyValuePair<string, TargetResult> targetResult in results._resultsByTarget)
    //         {
    //             // NOTE: I believe that because we only allow results for a given target to be produced and cached once for a given configuration,
    //             // we can never receive conflicting results for that target, since the cache and build request manager would always return the
    //             // cached results after the first time the target is built.  As such, we can allow "duplicates" to be merged in because there is
    //             // no change.  If, however, this turns out not to be the case, we need to re-evaluate this merging and possibly re-enable the
    //             // assertion below.
    //             // ErrorUtilities.VerifyThrow(!HasResultsForTarget(targetResult.Key), "Results already exist");
    //
    //             // Copy the new results in.
    //             _resultsByTarget[targetResult.Key] = targetResult.Value;
    //             if (results is not BazelBuildResult)
    //                 NewResults.Add(targetResult);
    //         }
    //
    //         // If there is an exception and we did not previously have one, add it in.
    //         _requestException ??= results.Exception;
    //     }
    //
    //     public override void AddResultsForTarget(string target, TargetResult result)
    //     {
    //         base.AddResultsForTarget(target, result);
    //         NewResults.Add(new KeyValuePair<string, TargetResult>(target, result));
    //     }
    // }

    public class BazelResultCache : ResultsCache
    {
        // public HashSet<BuildResult> NewResults { get; } = new HashSet<BuildResult>();
        // public override void AddResult(BuildResult result)
        // {
        //     lock (_resultsByConfiguration)
        //     {
        //         if (_resultsByConfiguration.ContainsKey(result.ConfigurationId))
        //         {
        //             if (Object.ReferenceEquals(_resultsByConfiguration[result.ConfigurationId], result))
        //             {
        //                 // Merging results would be meaningless as we would be merging the object with itself.
        //                 return;
        //             }
        //
        //             _resultsByConfiguration[result.ConfigurationId].MergeResults(result);
        //             if (result is not BazelBuildResult)
        //                 NewResults.Add(result);
        //         }
        //         else
        //         {
        //             // Note that we are not making a copy here.  This is by-design.  The TargetBuilder uses this behavior
        //             // to ensure that re-entering a project will be able to see all previously built targets and avoid
        //             // building them again.
        //             if (!_resultsByConfiguration.TryAdd(result.ConfigurationId, result))
        //             {
        //                 ErrorUtilities.ThrowInternalError("Failed to add result for configuration {0}", result.ConfigurationId);
        //             }
        //             if (result is not BazelBuildResult)
        //                 NewResults.Add(result);
        //         }
        //     }
        // }

        public override void ClearResultsForConfiguration(int configurationId)
        {
            // never do this
        }
    }
}