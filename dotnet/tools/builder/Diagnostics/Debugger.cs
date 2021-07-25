using System;
using System.Diagnostics;
using System.Threading;

namespace RulesMSBuild.Tools.Builder.Diagnostics
{
    public static class Debugger
    {
        public static void WaitForAttach(bool forceBreak = true)
        {
            if (System.Diagnostics.Debugger.IsAttached && !forceBreak) return;
            Process currentProcess = Process.GetCurrentProcess();
            Console.WriteLine($"Waiting for debugger to attach... ({currentProcess.MainModule!.FileName} PID {currentProcess.Id})");
            while (!System.Diagnostics.Debugger.IsAttached)
            {
                Thread.Sleep(100);
            }
            Console.WriteLine("debugger attached!");
            System.Diagnostics.Debugger.Break();
        }
    }
}