using System;
using System.Diagnostics;
using System.Threading;

namespace MyRulesDotnet.Tools.Builder
{
    public static class Diagnostics
    {
        public static void WaitForDebugger()
        {
            Process currentProcess = Process.GetCurrentProcess();
            Console.WriteLine($"Waiting for debugger to attach... ({currentProcess.MainModule.FileName} PID {currentProcess.Id})");
            while (!Debugger.IsAttached)
            {
                Thread.Sleep(100);
            }
            Console.WriteLine("debugger attached!");
            Debugger.Break();
        }
    }
}