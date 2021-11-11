#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using RulesMSBuild.Tools.Builder.Diagnostics;

namespace RulesMSBuild.Tools.Builder.MSBuild
{
    public interface IBazelMsBuildLogger : INodeLogger
    {
        bool HasError { get; }
        void Error(string message);
    }

    public class BazelMsBuildLogger : ConsoleLogger, IBazelMsBuildLogger
    {
        public bool HasError { get; set; }

        public void Error(string message) => _myWrite(message);

        private static WriteHandler Write(WriteHandler? other, Func<string, string> trimPath)
        {
            other ??= Console.Out.Write;
            return (m) =>
            {
                other((m));
                // other(trimPath(m));
            };
        }

        public BazelMsBuildLogger(
            WriteHandler? write,
            LoggerVerbosity verbosity, Func<string, string> trimPath)
            : base(verbosity, Write(write, trimPath),
                SetColor,
                ResetColor)
        {
            _myWrite = Write(write, trimPath);
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
                    Console.WriteLine(
                        "\n\tdo you need to execute `bazel run //:gazelle` to update your build files?\n");
                }
            };
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

        private readonly WriteHandler _myWrite;

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