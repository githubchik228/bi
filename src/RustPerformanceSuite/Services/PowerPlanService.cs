using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RustPerformanceSuite.Services;

public sealed class PowerPlanService
{
    private static (string Output, int ExitCode) Run(string args)
    {
        using var p = Process.Start(new ProcessStartInfo("powercfg.exe", args) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true });
        if (p is null) return ("", -1);
        p.WaitForExit(5000);
        return (p.StandardOutput.ReadToEnd().Trim(), p.ExitCode);
    }

    public string GetActiveScheme()
    {
        var (output, _) = Run("/getactivescheme");
        var match = Regex.Match(output, @"([0-9a-fA-F-]{36})");
        return match.Success ? match.Value : "";
    }

    public bool SetHighPerformance()
    {
        var (output, _) = Run("/list");
        var match = Regex.Match(output, @"([0-9a-fA-F-]{36}).*High performance", RegexOptions.IgnoreCase);
        if (!match.Success) return false;
        Run($"/setactive {match.Value}");
        return GetActiveScheme().Equals(match.Value, StringComparison.OrdinalIgnoreCase);
    }

    public bool Restore(string schemeGuid)
    {
        if (string.IsNullOrWhiteSpace(schemeGuid)) return false;
        var (_, code) = Run($"/setactive {schemeGuid}");
        return code == 0 && GetActiveScheme().Equals(schemeGuid, StringComparison.OrdinalIgnoreCase);
    }
}