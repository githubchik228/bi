using System.Diagnostics;

namespace RustPerformanceSuite.Services;

public sealed class PowerPlanService
{
    private static string Run(string args)
    {
        using var p = Process.Start(new ProcessStartInfo("powercfg.exe", args) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true });
        p?.WaitForExit(5000);
        return p?.StandardOutput.ReadToEnd().Trim() ?? "";
    }

    public string GetActiveScheme()
    {
        var output = Run("/getactivescheme");
        var match = System.Text.RegularExpressions.Regex.Match(output, @"([0-9a-fA-F-]{36})");
        return match.Success ? match.Value : "";
    }

    public bool SetHighPerformance()
    {
        var output = Run("/list");
        var match = System.Text.RegularExpressions.Regex.Match(output, @"([0-9a-fA-F-]{36}).*High performance", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success) return false;
        Run($"/setactive {match.Value}");
        return GetActiveScheme().Equals(match.Value, StringComparison.OrdinalIgnoreCase);
    }

    public bool Restore(string schemeGuid) => !string.IsNullOrWhiteSpace(schemeGuid) && Run($"/setactive {schemeGuid}").Length >= 0;
}