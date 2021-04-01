using System;
using System.Collections.Generic;
using System.Text;

namespace MyRulesDotnet.Tools
{
    internal static class Check
    {
        public static void Argument(bool condition, string error = "Argument is not valid", object argument = null)
        {
            if (condition) return;
            if (argument != null)
                error = string.Format(error, argument);
            throw new ArgumentException(error);
        }
    }
}
