#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace MyRulesDotnet.Tools.Builder
{
    public class BazelMsBuildLogger : ConsoleLogger
    {
        public BazelMsBuildLogger(LoggerVerbosity verbosity, string trimPath, TargetGraph? targetGraph) : base(
            verbosity,
            (m) => Console.Out.Write(m.Replace(trimPath, "")),
            SetColor,
            ResetColor)
        {
            _targetGraph = targetGraph;
        }

        public override void Initialize(IEventSource eventSource)
        {
            base.Initialize(eventSource);
            eventSource!.ErrorRaised += (sender, args) => HasError = true;
            if (_targetGraph != null)
            {
                eventSource.AnyEventRaised += ((sender, args) =>
                {
                    string? name = null;
                    string? parentName = null;
                    var wasSkipped = false;
                    var wasBuilt = false;
                    TargetBuiltReason reason;
                    switch (args)
                    {
                        case TargetSkippedEventArgs skipped:
                            name = skipped.TargetName;
                            parentName = skipped.ParentTarget;
                            wasSkipped = true;
                            reason = skipped.BuildReason;
                            break;
                        case TargetStartedEventArgs started:
                            name = started.TargetName;
                            parentName = started.ParentTarget;
                            wasBuilt = true;
                            reason = started.BuildReason;
                            break;
                        default:
                            return;
                    }

                    var node = _targetGraph.GetOrAdd(name);
                    node.WasBuilt = node.WasBuilt || wasBuilt;
                    if (parentName != null)
                    {
                        var parent = _targetGraph.GetOrAdd(parentName);
                        var edge = new TargetGraph.Edge(parent, node, wasSkipped);
                        parent.Dependencies.Add(edge);
                        edge.Reason = reason;
                    }
                });
            }
        }
        public override void Initialize(IEventSource eventSource, int nodeCount)
        {
            base.Initialize(eventSource, nodeCount);
            Initialize(eventSource);
        }

        public bool HasError { get; set; }

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

        private TargetGraph? _targetGraph;

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