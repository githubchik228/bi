using System.Diagnostics;

namespace RustPerformanceSuite.Services;

public sealed class SystemMonitor : IDisposable
{
    private readonly PerformanceCounter? _cpu;
    private readonly Timer _timer;
    public event Action<double>? CpuUpdated;
    public event Action<double>? RamUpdated;

    public SystemMonitor()
    {
        try { _cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total"); _cpu.NextValue(); } catch { }
        _timer = new Timer(Update, null, 1000, 1000);
    }

    private void Update(object? state)
    {
        try { CpuUpdated?.Invoke(Math.Round(_cpu?.NextValue() ?? 0, 1)); } catch { }
        var info = GC.GetGCMemoryInfo();
        var used = Process.GetCurrentProcess().WorkingSet64 / 1024d / 1024d;
        RamUpdated?.Invoke(Math.Round(used, 1));
    }

    public void Dispose() => _timer.Dispose();
}
