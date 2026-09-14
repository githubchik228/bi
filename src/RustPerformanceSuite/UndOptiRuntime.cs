using System.Diagnostics;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace RustPerformanceSuite;

public sealed record ChangeRecord(string Id, string Target, string Before, string After, DateTime Utc);
public sealed record BenchmarkResult(double AverageFps, double OnePercentLow, double AverageFrameTimeMs, int Samples);

public sealed class UndOptiRuntime
{
    public static UndOptiRuntime Instance { get; } = new();
    public string Root { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UndOpti");
    public string ChangesFile => Path.Combine(Root, "changes.json");
    public List<ChangeRecord> Changes { get; private set; } = [];
    public string HardwareId { get; }

    private UndOptiRuntime()
    {
        Directory.CreateDirectory(Root);
        HardwareId = CreateHardwareId();
        Load();
    }

    void Load()
    {
        try
        {
            if (File.Exists(ChangesFile))
                Changes = JsonSerializer.Deserialize<List<ChangeRecord>>(File.ReadAllText(ChangesFile)) ?? [];
        }
        catch { Changes = []; }
    }

    void SaveChanges() => File.WriteAllText(ChangesFile, JsonSerializer.Serialize(Changes, new JsonSerializerOptions { WriteIndented = true }));

    static string CreateHardwareId()
    {
        var s = $"{Environment.MachineName}|{Environment.OSVersion}|{Environment.ProcessorCount}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)))[..16];
    }

    public void ApplySafeProfile()
    {
        SetDword("GameMode", @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1);
        SetDword("GameDVR", @"SystemGameConfigStore", "GameDVR_Enabled", 0);
        SetDword("AppCapture", @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0);
        SetDword("Transparency", @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0);
    }

    void SetDword(string id, string path, string name, int value)
    {
        using var k = Registry.CurrentUser.CreateSubKey(path, true);
        if (k is null) return;
        var before = k.GetValue(name, null)?.ToString() ?? "<missing>";
        var after = value.ToString();
        if (before == after) return;
        k.SetValue(name, value, RegistryValueKind.DWord);
        Changes.RemoveAll(x => x.Id == id);
        Changes.Add(new ChangeRecord(id, $"HKCU\\{path}\\{name}", before, after, DateTime.UtcNow));
        SaveChanges();
    }

    public int Restore()
    {
        var n = 0;
        foreach (var c in Changes.ToArray().Reverse())
        {
            if (!c.Target.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase)) continue;
            var p = c.Target[5..].Split('\\');
            if (p.Length < 2) continue;
            var name = p[^1];
            var sub = string.Join('\\', p[..^1]);
            using var k = Registry.CurrentUser.OpenSubKey(sub, true);
            if (k is null) continue;
            var now = k.GetValue(name, null)?.ToString() ?? "<missing>";
            if (now != c.After) continue;
            if (c.Before == "<missing>") k.DeleteValue(name, false);
            else if (int.TryParse(c.Before, out var v)) k.SetValue(name, v, RegistryValueKind.DWord);
            Changes.Remove(c);
            n++;
        }
        SaveChanges();
        return n;
    }

    public bool RustRunning() => Process.GetProcessesByName("RustClient_Win64").Length > 0 || Process.GetProcessesByName("RustClient").Length > 0;

    public BenchmarkResult Benchmark(IEnumerable<double> frameTimes)
    {
        var a = frameTimes.Where(x => x > 0).OrderBy(x => x).ToArray();
        if (a.Length == 0) return new(0, 0, 0, 0);
        var avg = a.Average();
        var fps = 1000.0 / avg;
        var count = Math.Max(1, (int)Math.Ceiling(a.Length * .01));
        var low = 1000.0 / a[^count..].Average();
        return new(fps, low, avg, a.Length);
    }

    public string HardwareSummary()
    {
        try
        {
            using var cpuSearch = new ManagementObjectSearcher("SELECT Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed FROM Win32_Processor");
            using var gpuSearch = new ManagementObjectSearcher("SELECT Name,DriverVersion FROM Win32_VideoController");
            using var boardSearch = new ManagementObjectSearcher("SELECT Manufacturer,Product FROM Win32_BaseBoard");
            var cpu = cpuSearch.Get().Cast<ManagementObject>().FirstOrDefault();
            var gpu = gpuSearch.Get().Cast<ManagementObject>().FirstOrDefault();
            var board = boardSearch.Get().Cast<ManagementObject>().FirstOrDefault();
            var ram = Math.Round(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1073741824d, 1);
            return $"CPU: {cpu?["Name"] ?? "Unknown"}\nCores/Threads: {cpu?["NumberOfCores"] ?? "?"}/{cpu?["NumberOfLogicalProcessors"] ?? "?"}\nMax clock: {cpu?["MaxClockSpeed"] ?? "?"} MHz\nGPU: {gpu?["Name"] ?? "Unknown"}\nDriver: {gpu?["DriverVersion"] ?? "?"}\nBoard: {board?["Manufacturer"]} {board?["Product"]}\nRAM: {ram} GB\nOS: {Environment.OSVersion}";
        }
        catch { return "Hardware information unavailable."; }
    }
}
