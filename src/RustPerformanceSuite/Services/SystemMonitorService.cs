using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RustPerformanceSuite.Services;

public sealed class SystemMonitorService
{
    [StructLayout(LayoutKind.Sequential)] private struct MEMORYSTATUSEX { public uint Length; public uint MemoryLoad; public ulong TotalPhys; public ulong AvailPhys; public ulong TotalPageFile; public ulong AvailPageFile; public ulong TotalVirtual; public ulong AvailVirtual; public ulong AvailExtendedVirtual; }
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
    private readonly Process _process = Process.GetCurrentProcess();
    private TimeSpan _lastCpu;
    private DateTime _lastSample = DateTime.UtcNow;

    public (double CpuPercent, double MemoryPercent) Sample()
    {
        _process.Refresh();
        var now = DateTime.UtcNow;
        var cpu = _process.TotalProcessorTime;
        var elapsed = (now - _lastSample).TotalMilliseconds;
        var cpuPercent = elapsed <= 0 ? 0 : Math.Clamp((cpu - _lastCpu).TotalMilliseconds / (elapsed * Environment.ProcessorCount) * 100, 0, 100);
        _lastCpu = cpu; _lastSample = now;
        var mem = new MEMORYSTATUSEX { Length = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        GlobalMemoryStatusEx(ref mem);
        return (cpuPercent, mem.TotalPhys == 0 ? 0 : (1 - (double)mem.AvailPhys / mem.TotalPhys) * 100);
    }
}