using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace MyRulesDotnet.Tools.Builder
{
    public class BazelMsBuildLogger : ConsoleLogger
    {
        public BazelMsBuildLogger(string trimPath) : base(
            LoggerVerbosity.Normal,
            (m) => Console.Out.Write(m.Replace(trimPath, "")),
            SetColor,
            ResetColor)
        {
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