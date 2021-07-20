using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;

namespace RulesMSBuild.Tools.Builder.Diagnostics
{
    public class TargetGraphLogger : ILogger
    {
        private readonly PathMapper _pathMapper;
        public LoggerVerbosity Verbosity { get; set; }
        public string Parameters { get; set; }
        
        private readonly TargetGraph _targetGraph;
        private readonly Stack<Cluster> _projectStack = new Stack<Cluster>();
        private Cluster? _cluster;
        private readonly Stack<string> _targetStack = new Stack<string>();
        private bool _hasError;

        public TargetGraphLogger(TargetGraph targetGraph, PathMapper pathMapper)
        {
            _pathMapper = pathMapper;
            _targetGraph = targetGraph;
        }
        
        public void Initialize(IEventSource eventSource)
        {
            eventSource.AnyEventRaised += AnyEvent;
            eventSource.ErrorRaised += (_, __) => _hasError = true;
        }

        private void AnyEvent(object sender, BuildEventArgs args)
        {
            switch (args)
            {
                case ProjectStartedEventArgs pStart:
                    BazelLogger.Debug(
                        $"Building project {pStart.ProjectFile}\n\t{string.Join("\n\t", pStart.GlobalProperties.Select(p => $"{p.Key}: {p.Value}"))}");
                    var clusterNameS = _pathMapper.ToBazel(pStart.ProjectFile);
                    var cluster = _targetGraph!.GetOrAddCluster(clusterNameS, pStart.GlobalProperties);
                    if (_cluster != null)
                        _projectStack.Push(_cluster);
                    _cluster = cluster;
                    return;
                case ProjectFinishedEventArgs pEnd:
                    var clusterNameE = _pathMapper.ToBazel(pEnd.ProjectFile);
                    if (_cluster!.Name != clusterNameE) throw new Exception(":(");
                    _projectStack.TryPop(out _cluster);
                    return;
                case TargetSkippedEventArgs skipped:
                    var s = AddNode(
                        skipped.TargetName,
                        true,
                        skipped.ParentTarget,
                        skipped.BuildReason,
                        skipped.ProjectFile
                    );

                    if (_hasError)
                        s.Error = true;
                    break;
                case TargetStartedEventArgs started:
                    var node = AddNode(
                        started.TargetName,
                        false,
                        started.ParentTarget,
                        started.BuildReason,
                        started.ProjectFile
                    );
                    node.Started = true;
                    break;
                case TargetFinishedEventArgs finished:
                    if (_targetStack.Peek() != finished.TargetName) throw new Exception(":(");
                    _targetStack.Pop();
                    var n = _cluster!.GetOrAdd(finished.TargetName);
                    
                    n.Finished = true;
                    if (_hasError)
                        n.Error = true;
                    return;
                default:
                    return;
            }
        }

        private TargetGraph.Node AddNode(string name, bool wasSkipped, string? parentName, TargetBuiltReason reason, string projectFile)
        {
            _targetStack.TryPeek(out var stackParent);
            if (!wasSkipped)
                _targetStack.Push(name);

            if (_cluster == null)
                throw new Exception($"Cluster is null!");
            
            var node = _cluster!.GetOrAdd(name);
            
            node.Finished = wasSkipped;
            // was the MSBuild task called on this target directly?
            
            bool forced = false;
            if (parentName == null)
            {
                forced = true;
                parentName = stackParent;
            }
            if (parentName != null)
            {
                Cluster? parentCluster = null;
                if (forced)
                    _projectStack.TryPeek(out parentCluster);
                var parent = (parentCluster ?? _cluster).GetOrAdd(parentName);
                var edge = parent.AddDependency(node, reason);
                edge.Runtime = true;
                edge.Forced = forced;
            }

            return node;
        }
        
        public void Shutdown()
        {
        }
    }
}