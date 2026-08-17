using System.Text.Json;

namespace RustPerformanceSuite.License;

public sealed class LicenseManager
{
    private readonly string _path;
    private readonly HardwareIdService _hardware;
    public LicenseInfo? Current { get; private set; }

    public LicenseManager(HardwareIdService hardware)
    {
        _hardware = hardware;
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UndOpti");
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "license.json");
        Load();
    }

    public string HardwareId => _hardware.GetHardwareId();
    public bool IsActive => Current?.IsActive == true && Current.HardwareId == HardwareId;
    public bool HasExpired => Current is not null && !IsActive;

    public bool ActivateLocalDemo(string key, string plan)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        var duration = plan.ToLowerInvariant() switch
        {
            "1d" => TimeSpan.FromDays(1),
            "7d" => TimeSpan.FromDays(7),
            "30d" => TimeSpan.FromDays(30),
            "1y" => TimeSpan.FromDays(365),
            "lifetime" => TimeSpan.MaxValue,
            _ => TimeSpan.Zero
        };
        if (duration == TimeSpan.Zero) return false;
        var now = DateTime.UtcNow;
        var expires = duration == TimeSpan.MaxValue ? DateTime.MaxValue : now.Add(duration);
        Current = new LicenseInfo(key.Trim(), plan, now, expires, HardwareId);
        Save();
        return true;
    }

    public void ClearExpired()
    {
        if (HasExpired) { Current = null; Save(); }
    }

    private void Load()
    {
        try { if (File.Exists(_path)) Current = JsonSerializer.Deserialize<LicenseInfo>(File.ReadAllText(_path)); }
        catch { Current = null; }
    }

    private void Save() => File.WriteAllText(_path, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
}
