using System.Diagnostics;
using Microsoft.Win32;

namespace RustPerformanceSuite.Services;

public sealed record OptimizationResult(string Category, string Action, bool Changed, string Details);

public sealed class OptimizationSuite
{
    public IReadOnlyList<OptimizationResult> OptimizeWindows()
    {
        var r = new List<OptimizationResult>();
        r.Add(SetDword("Windows", Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1));
        r.Add(SetDword("Windows", Registry.CurrentUser, @"SystemGameConfigStore", "GameDVR_Enabled", 0));
        r.Add(SetDword("Windows", Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0));
        r.Add(SetDword("Windows", Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0));
        return r;
    }

    public IReadOnlyList<OptimizationResult> CleanTempFiles()
    {
        var r = new List<OptimizationResult>();
        foreach (var dir in new[] { Path.GetTempPath(), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp") })
        {
            var removed = 0;
            try
            {
                if (Directory.Exists(dir))
                    foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly))
                        try { File.Delete(file); removed++; } catch { }
                r.Add(new("Cleaner", "Temporary files", removed > 0, $"Removed {removed} files from {dir}"));
            }
            catch (Exception ex) { r.Add(new("Cleaner", "Temporary files", false, ex.Message)); }
        }
        return r;
    }

    public OptimizationResult FlushDns()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("ipconfig.exe", "/flushdns") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true });
            if (p is null) return new("Network", "Flush DNS", false, "Could not start ipconfig.");
            p.WaitForExit(8000);
            return new("Network", "Flush DNS", p.ExitCode == 0, p.StandardOutput.ReadToEnd().Trim());
        }
        catch (Exception ex) { return new("Network", "Flush DNS", false, ex.Message); }
    }

    public OptimizationResult SelectHighPerformancePowerPlan()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("powercfg.exe", "/setactive SCHEME_MIN") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true });
            if (p is null) return new("Power", "High performance plan", false, "Could not start powercfg.");
            p.WaitForExit(8000);
            return new("Power", "High performance plan", p.ExitCode == 0, "Windows high-performance scheme selected.");
        }
        catch (Exception ex) { return new("Power", "High performance plan", false, ex.Message); }
    }

    public string AdvancedHardwareReport()
    {
        try
        {
            using var cpu = new System.Management.ManagementObjectSearcher("SELECT Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed FROM Win32_Processor");
            using var board = new System.Management.ManagementObjectSearcher("SELECT Manufacturer,Product FROM Win32_BaseBoard");
            using var gpu = new System.Management.ManagementObjectSearcher("SELECT Name,AdapterRAM,DriverVersion FROM Win32_VideoController");
            var c = cpu.Get().Cast<System.Management.ManagementObject>().FirstOrDefault();
            var b = board.Get().Cast<System.Management.ManagementObject>().FirstOrDefault();
            var g = gpu.Get().Cast<System.Management.ManagementObject>().FirstOrDefault();
            return $"CPU: {c?["Name"]}\nCores / Threads: {c?["NumberOfCores"]} / {c?["NumberOfLogicalProcessors"]}\nMax clock: {c?["MaxClockSpeed"]} MHz\nMotherboard: {b?["Manufacturer"]} {b?["Product"]}\nGPU: {g?["Name"]}\nGPU driver: {g?["DriverVersion"]}\n\nOC / undervolt advisor: ready to analyze exact hardware. No blind voltage or clock writes are performed.";
        }
        catch (Exception ex) { return "Hardware scan failed: " + ex.Message; }
    }

    static OptimizationResult SetDword(string category, RegistryKey root, string path, string name, int value)
    {
        try
        {
            using var key = root.CreateSubKey(path, true);
            if (key is null) return new(category, name, false, "Registry access unavailable.");
            var before = key.GetValue(name, null);
            if (before is int i && i == value) return new(category, name, false, "Already configured.");
            key.SetValue(name, value, RegistryValueKind.DWord);
            return new(category, name, true, $"Set to {value}; previous value: {before ?? "missing"}.");
        }
        catch (Exception ex) { return new(category, name, false, ex.Message); }
    }
}
