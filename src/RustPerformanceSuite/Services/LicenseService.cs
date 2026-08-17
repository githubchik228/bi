using System.Text.Json;
using RustPerformanceSuite.Models;

namespace RustPerformanceSuite.Services;

public sealed class LicenseService
{
    private readonly string _file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RustPerformanceSuite", "license.json");
    private readonly HardwareIdService _hardwareId = new();
    public LicenseInfo? Current { get; private set; }
    public bool IsLicensed => Current?.IsActive == true && Current.HardwareId == _hardwareId.GetHardwareId();

    public LicenseService() => Load();

    public void Load()
    {
        try { if (File.Exists(_file)) Current = JsonSerializer.Deserialize<LicenseInfo>(File.ReadAllText(_file)); }
        catch { Current = null; }
    }

    // Server validation belongs here. Never embed a private signing secret in the client.
    public bool Activate(LicenseInfo license)
    {
        if (string.IsNullOrWhiteSpace(license.Key) || license.HardwareId != _hardwareId.GetHardwareId()) return false;
        Current = license;
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, JsonSerializer.Serialize(license, new JsonSerializerOptions { WriteIndented = true }));
        return true;
    }

    public bool HasExpired() => Current is not null && !Current.IsActive;
}