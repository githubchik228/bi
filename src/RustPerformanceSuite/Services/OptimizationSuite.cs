using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;
using Microsoft.Win32;

namespace RustPerformanceSuite.Services;

public sealed record OptimizationResult(string Category, string Action, bool Changed, string Details);

public sealed class OptimizationSuite
{
    private readonly UndOptiRuntime _runtime = UndOptiRuntime.Instance;
    private string PowerBackupFile => Path.Combine(_runtime.Root, "power-plan-backup.txt");

    public IReadOnlyList<OptimizationResult> OptimizeWindows()
    {
        var before = _runtime.Changes.Count;
        _runtime.ApplySafeProfile();
        var changed = _runtime.Changes.Count - before;
        return new[] { new OptimizationResult("Windows", "Gaming profile", changed > 0, $"Tracked Windows gaming tweaks applied: {changed} new change(s).") };
    }

    public IReadOnlyList<OptimizationResult> OptimizeRust()
    {
        var r = new List<OptimizationResult>();
        r.AddRange(OptimizeWindows());
        r.Add(SetRustPriority());
        return r;
    }

    public IReadOnlyList<OptimizationResult> CleanTempFiles()
    {
        var r = new List<OptimizationResult>();
        foreach (var dir in new[]
        {
            Path.GetTempPath(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp")
        })
        {
            var removed = 0;
            try
            {
                if (Directory.Exists(dir))
                {
                    foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly))
                        try { File.Delete(file); removed++; } catch { }
                    foreach (var sub in Directory.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly))
                        try { Directory.Delete(sub, true); removed++; } catch { }
                }
                r.Add(new("Cleaner", "Temporary files", removed > 0, $"Processed {dir}; removed {removed} item(s)."));
            }
            catch (Exception ex) { r.Add(new("Cleaner", "Temporary files", false, ex.Message)); }
        }

        r.Add(CleanShaderCache());
        return r;
    }

    public OptimizationResult CleanShaderCache()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache");
        var removed = 0;
        try
        {
            if (!Directory.Exists(dir))
                return new("Cleaner", "DirectX shader cache", false, "Shader cache directory not found.");

            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly))
                try { File.Delete(file); removed++; } catch { }

            return new("Cleaner", "DirectX shader cache", removed > 0, $"Removed {removed} cached file(s). The cache will rebuild when games run.");
        }
        catch (Exception ex) { return new("Cleaner", "DirectX shader cache", false, ex.Message); }
    }

    public OptimizationResult FlushDns()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("ipconfig.exe", "/flushdns")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (p is null) return new("Network", "Flush DNS", false, "Could not start ipconfig.");
            p.WaitForExit(8000);
            var output = p.StandardOutput.ReadToEnd().Trim();
            var error = p.StandardError.ReadToEnd().Trim();
            return new("Network", "Flush DNS", p.ExitCode == 0, string.IsNullOrWhiteSpace(output) ? error : output);
        }
        catch (Exception ex) { return new("Network", "Flush DNS", false, ex.Message); }
    }

    public OptimizationResult SelectHighPerformancePowerPlan()
    {
        try
        {
            if (!File.Exists(PowerBackupFile))
            {
                var current = Run("powercfg.exe", "/getactivescheme");
                var guid = ExtractGuid(current);
                if (!string.IsNullOrWhiteSpace(guid))
                    File.WriteAllText(PowerBackupFile, guid);
            }

            var result = Run("powercfg.exe", "/setactive SCHEME_MIN");
            return new("Power", "High performance plan", result.ExitCode == 0,
                result.ExitCode == 0 ? "High-performance scheme selected. Previous scheme is saved for restore." : result.Output);
        }
        catch (Exception ex) { return new("Power", "High performance plan", false, ex.Message); }
    }

    public OptimizationResult RestorePowerPlan()
    {
        try
        {
            if (!File.Exists(PowerBackupFile))
                return new("Power", "Restore power plan", false, "No saved power plan was found.");
            var guid = File.ReadAllText(PowerBackupFile).Trim();
            if (string.IsNullOrWhiteSpace(guid)) return new("Power", "Restore power plan", false, "Saved plan is invalid.");
            var result = Run("powercfg.exe", $"/setactive {guid}");
            if (result.ExitCode == 0) File.Delete(PowerBackupFile);
            return new("Power", "Restore power plan", result.ExitCode == 0, result.ExitCode == 0 ? "Previous power plan restored." : result.Output);
        }
        catch (Exception ex) { return new("Power", "Restore power plan", false, ex.Message); }
    }

    public OptimizationResult SetRustPriority()
    {
        try
        {
            var processes = Process.GetProcessesByName("RustClient_Win64").Concat(Process.GetProcessesByName("RustClient")).ToArray();
            if (processes.Length == 0) return new("Rust", "Process priority", false, "Rust is not running. Start Rust and run this action again.");
            var changed = 0;
            foreach (var p in processes)
            {
                try
                {
                    if (p.PriorityClass != ProcessPriorityClass.AboveNormal)
                    {
                        p.PriorityClass = ProcessPriorityClass.AboveNormal;
                        changed++;
                    }
                }
                catch { }
                finally { p.Dispose(); }
            }
            return new("Rust", "Process priority", changed > 0, $"Set Rust to AboveNormal for {changed} process(es). This is session-only and does not modify Rust files.");
        }
        catch (Exception ex) { return new("Rust", "Process priority", false, ex.Message); }
    }

    public string StartupReport()
    {
        var lines = new List<string>();
        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (key is null) continue;
                foreach (var name in key.GetValueNames())
                    lines.Add($"{(root == Registry.CurrentUser ? "User" : "Machine")}: {name} = {key.GetValue(name)}");
            }
            catch { }
        }
        return lines.Count == 0 ? "No classic Run startup entries found." : string.Join(Environment.NewLine, lines);
    }

    public string NetworkReport()
    {
        var lines = new List<string>();
        foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (n.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            lines.Add($"{n.Name} — {n.NetworkInterfaceType} — {n.OperationalStatus} — {n.Speed / 1_000_000d:0} Mbps");
        }
        try
        {
            using var ping = new Ping();
            var reply = ping.Send("1.1.1.1", 1200);
            lines.Add(reply.Status == IPStatus.Success ? $"1.1.1.1 latency: {reply.RoundtripTime} ms" : $"1.1.1.1 ping: {reply.Status}");
        }
        catch (Exception ex) { lines.Add("Ping unavailable: " + ex.Message); }
        return string.Join(Environment.NewLine, lines);
    }

    public string AdvancedHardwareReport()
    {
        try
        {
            using var cpu = new ManagementObjectSearcher("SELECT Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed FROM Win32_Processor");
            using var board = new ManagementObjectSearcher("SELECT Manufacturer,Product FROM Win32_BaseBoard");
            using var gpu = new ManagementObjectSearcher("SELECT Name,AdapterRAM,DriverVersion FROM Win32_VideoController");
            var c = cpu.Get().Cast<ManagementObject>().FirstOrDefault();
            var b = board.Get().Cast<ManagementObject>().FirstOrDefault();
            var g = gpu.Get().Cast<ManagementObject>().FirstOrDefault();
            return $"CPU: {c?["Name"]}\nCores / Threads: {c?["NumberOfCores"]} / {c?["NumberOfLogicalProcessors"]}\nMax clock: {c?["MaxClockSpeed"]} MHz\nMotherboard: {b?["Manufacturer"]} {b?["Product"]}\nGPU: {g?["Name"]}\nGPU driver: {g?["DriverVersion"]}\n\nBIOS / OC advisor:\n• XMP/EXPO: check BIOS memory profile and verify stability after enabling.\n• CPU tuning: use the motherboard vendor's documented controls; no blind voltage writes.\n• GPU tuning: use the official GPU vendor utility and change one value at a time.\n• BIOS firmware: manual/vendor-guided only; UndOpti never flashes firmware automatically.";
        }
        catch (Exception ex) { return "Hardware scan failed: " + ex.Message; }
    }

    public string PerformanceRecommendations()
    {
        var hw = new HardwareAnalyzer().Analyze();
        var tips = new List<string>
        {
            "Use the Windows gaming profile first, then measure before/after FPS and frametime.",
            "Keep GPU drivers current and prefer a clean driver install when troubleshooting stutter.",
            "For Rust, prioritize stable frametimes over a peak FPS number.",
            "Do not disable Windows security, Defender, EAC, or core isolation just for benchmark numbers."
        };
        if (hw.RamGb < 16) tips.Add("Detected RAM is below 16 GB; memory capacity may be a bottleneck for Rust and background apps.");
        if (hw.Gpu.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)) tips.Add("The active WMI GPU entry looks generic; verify the real GPU in Device Manager.");
        return string.Join(Environment.NewLine, tips.Select(x => "• " + x));
    }

    static (int ExitCode, string Output) Run(string file, string args)
    {
        using var p = Process.Start(new ProcessStartInfo(file, args)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });
        if (p is null) return (-1, "Could not start process.");
        p.WaitForExit(8000);
        var output = p.StandardOutput.ReadToEnd().Trim();
        if (string.IsNullOrWhiteSpace(output)) output = p.StandardError.ReadToEnd().Trim();
        return (p.ExitCode, output);
    }

    static string ExtractGuid(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        return match.Success ? match.Value : string.Empty;
    }
}
