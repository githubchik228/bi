using System.Diagnostics;

namespace RustPerformanceSuite.Services;

public sealed class SystemMonitor : IDisposable
{
    private readonly Timer _timer;
    private TimeSpan _lastCpu;
    private DateTime _lastSample;
    public event Action<double>? CpuUpdated;
    public event Action<double>? RamUpdated;

    public SystemMonitor()
    {
        _lastCpu = Process.GetCurrentProcess().TotalProcessorTime;
        _lastSample = DateTime.UtcNow;
        _timer = new Timer(Update, null, 1000, 1000);
    }

    private void Update(object? state)
    {
        try
        {
            using var p = Process.GetCurrentProcess();
            var now = DateTime.UtcNow;
            var cpu = p.TotalProcessorTime;
            var wallMs = Math.Max(1, (now - _lastSample).TotalMilliseconds);
            var cpuMs = (cpu - _lastCpu).TotalMilliseconds;
            var percent = Math.Clamp(cpuMs / (wallMs * Environment.ProcessorCount) * 100.0, 0, 100);
            _lastCpu = cpu;
            _lastSample = now;
            CpuUpdated?.Invoke(Math.Round(percent, 1));
            RamUpdated?.Invoke(Math.Round(p.WorkingSet64 / 1024d / 1024d, 1));
        }
        catch { }
    }

    public void Dispose() => _timer.Dispose();
}
