#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using RulesMSBuild.Tools.Builder.Diagnostics;

namespace RulesMSBuild.Tools.Builder.MSBuild
{
    public class BazelMsBuildLogger : ConsoleLogger
    {
        private readonly Func<string, string> _trimPath;
        private readonly TargetGraph? _targetGraph;
        private readonly Stack<Cluster> _projectStack = new Stack<Cluster>();
        private Cluster? _cluster;
        private Stack<string> _targetStack = new Stack<string>();
        public bool HasError { get; set; }

        public BazelMsBuildLogger(LoggerVerbosity verbosity, Func<string,string> trimPath, TargetGraph? targetGraph) 
            : base(verbosity, (m) => Console.Out.Write(trimPath(m)),
            SetColor,
            ResetColor)
        {
            _trimPath = trimPath;
            _targetGraph = targetGraph;
            _cluster = targetGraph;
        }

        public override void Initialize(IEventSource eventSource)
        {
            base.Initialize(eventSource);
            InitializeImpl(eventSource);
        }
        
        public override void Initialize(IEventSource eventSource, int nodeCount)
        {
            base.Initialize(eventSource, nodeCount);
            InitializeImpl(eventSource);
        }
        
        private void InitializeImpl(IEventSource eventSource)
        {
            eventSource!.ErrorRaised += (sender, args) =>
            {
                HasError = true;
                if (
                    args.Message.Contains("are you missing an assembly reference?") 
                    || args.Message.Contains(
                        "The project file could not be loaded. Could not find a part of the path")
                    )
                {
                    Console.WriteLine("\n\tdo you need to execute `bazel run //:gazelle` to update your build files?\n");
                } 
            };
            if (_targetGraph != null)
            {
                eventSource.AnyEventRaised += AnyEvent;
            }
        }

        private void AnyEvent(object sender, BuildEventArgs args)
        {
            switch (args)
            {
                case ProjectStartedEventArgs pStart:
                    var clusterNameS = _trimPath(pStart.ProjectFile);
                    var cluster = _targetGraph!.GetOrAddCluster(clusterNameS, pStart.GlobalProperties);
                    if (_cluster != null)
                        _projectStack.Push(_cluster);
                    _cluster = cluster;
                    return;
                case ProjectFinishedEventArgs pEnd:
                    var clusterNameE = _trimPath(pEnd.ProjectFile);
                    if (_cluster!.Name != clusterNameE) throw new Exception(":(");
                    _projectStack.TryPop(out _cluster);
                    
                    return;
                case TargetSkippedEventArgs skipped:
                    AddNode(
                        skipped.TargetName,
                        true,
                        skipped.ParentTarget,
                        skipped.BuildReason,
                        skipped.ProjectFile
                        );
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

        /// <summary>
        /// Sets foreground color to color specified
        /// </summary>
        internal static void SetColor(ConsoleColor c)
        {
            try
            {
                Console.ForegroundColor = TransformColor(c, BackgroundColor);
            }
            catch (IOException)
            {
                // Does not matter if we cannot set the color
            }
        }

        /// <summary>
        /// When set, we'll try reading background color.
        /// </summary>
        private static bool _supportReadingBackgroundColor = true;
        
        /// <summary>
        /// Some platforms do not allow getting current background color. There
        /// is not way to check, but not-supported exception is thrown. Assume
        /// black, but don't crash.
        /// </summary>
        internal static ConsoleColor BackgroundColor
        {
            get
            {
                if (_supportReadingBackgroundColor)
                {
                    try
                    {
                        return Console.BackgroundColor;
                    }
                    catch (PlatformNotSupportedException)
                    {
                        _supportReadingBackgroundColor = false;
                    }
                }

                return ConsoleColor.Black;
            }
        }

        /// <summary>
        /// Resets the color
        /// </summary>
        internal static void ResetColor()
        {
            try
            {
                Console.ResetColor();
            }
            catch (IOException)
            {
                // The color could not be reset, no reason to crash
            }
        }

        /// <summary>
        /// Changes the foreground color to black if the foreground is the
        /// same as the background. Changes the foreground to white if the
        /// background is black.
        /// </summary>
        /// <param name="foreground">foreground color for black</param>
        /// <param name="background">current background</param>
        internal static ConsoleColor TransformColor(ConsoleColor foreground, ConsoleColor background)
        {
            ConsoleColor result = foreground; //typically do nothing ...

            if (foreground == background)
            {
                result = background != ConsoleColor.Black ? ConsoleColor.Black : ConsoleColor.Gray;
            }

            return result;
        }
    }
}