using Microsoft.Win32;

namespace RustPerformanceSuite.Core;

public sealed class OptimizationEngine
{
    private readonly ChangeTracker _tracker;
    public OptimizationEngine(ChangeTracker tracker) => _tracker = tracker;

    public int Count => _tracker.Changes.Count;

    public void ApplySafeWindowsProfile()
    {
        ApplyRegistryDword("GameMode", RegistryHive.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1);
        ApplyRegistryDword("GameDvr", RegistryHive.CurrentUser, @"SystemGameConfigStore", "GameDVR_Enabled", 0);
        ApplyRegistryDword("GameDvrPolicy", RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0);
        ApplyRegistryDword("Transparency", RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0);
    }

    private void ApplyRegistryDword(string id, RegistryHive hive, string subKey, string name, int value)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.CreateSubKey(subKey, true);
        if (key is null) return;
        var original = key.GetValue(name, null)?.ToString() ?? "<missing>";
        var applied = value.ToString();
        if (original == applied) return;
        key.SetValue(name, value, RegistryValueKind.DWord);
        _tracker.Track(new TrackedChange(id, "Windows", $"HKCU\\{subKey}\\{name}", original, applied, DateTime.UtcNow));
    }

    public void RestoreAll()
    {
        foreach (var change in _tracker.Changes.Reverse())
        {
            if (!change.Target.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase)) continue;
            var parts = change.Target[5..].Split('\\');
            if (parts.Length < 2) continue;
            var name = parts[^1];
            var subKey = string.Join('\\', parts[..^1]);
            using var key = Registry.CurrentUser.OpenSubKey(subKey, writable: true);
            if (key is null) continue;
            var current = key.GetValue(name, null)?.ToString() ?? "<missing>";
            if (current != change.AppliedValue) continue;
            if (change.OriginalValue == "<missing>") key.DeleteValue(name, false);
            else if (int.TryParse(change.OriginalValue, out var number)) key.SetValue(name, number, RegistryValueKind.DWord);
            _tracker.Remove(change.Id);
        }
    }
}
