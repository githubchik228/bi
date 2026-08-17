using System.Management;

namespace RustPerformanceSuite.Services;

public sealed record HardwareReport(string Cpu, string Gpu, double RamGb, string Os, string Notes);

public sealed class HardwareAnalyzer
{
    public HardwareReport Analyze()
    {
        string cpu = "Unknown";
        string gpu = "Unknown";
        try
        {
            using var c = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            cpu = c.Get().Cast<ManagementObject>().FirstOrDefault()?["Name"]?.ToString() ?? cpu;
            using var g = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            gpu = g.Get().Cast<ManagementObject>().FirstOrDefault()?["Name"]?.ToString() ?? gpu;
        }
        catch { }
        var ram = Math.Round(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1073741824d, 1);
        var notes = "BIOS is analyzed only; UndOpti never flashes firmware automatically.";
        return new(cpu, gpu, ram, Environment.OSVersion.VersionString, notes);
    }
}
