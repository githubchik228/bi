using System.Diagnostics;

namespace RustPerformanceSuite.Services;

public sealed class RustDetector
{
    public bool IsRunning => Process.GetProcessesByName("RustClient_Win64").Length > 0;
    public string Status => IsRunning ? "Running" : "Not running";
}
