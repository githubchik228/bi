using System.Text.Json;
using RustPerformanceSuite.Models;

namespace RustPerformanceSuite.Services;

public sealed class LicenseService
{
    private readonly string _file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UndOpti", "license.json");
    private readonly HardwareIdService _hardwareId = new();
    public LicenseInfo? Current { get; private set; }
    public string HardwareId => _hardwareId.GetHardwareId();
    public bool IsLicensed => Current?.IsActive == true && Current.HardwareId == HardwareId;
    public bool IsExpired => Current is not null && !Current.IsActive;

    public LicenseService() => Load();
    public void Load()
    {
        try { if (File.Exists(_file)) Current = JsonSerializer.Deserialize<LicenseInfo>(File.ReadAllText(_file)); }
        catch { Current = null; }
    }

    public bool Activate(LicenseInfo license)
    {
        if (string.IsNullOrWhiteSpace(license.Key) || !license.HardwareId.Equals(HardwareId, StringComparison.OrdinalIgnoreCase) || !license.IsActive) return false;
        Current = license;
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, JsonSerializer.Serialize(license, new JsonSerializerOptions { WriteIndented = true }));
        return true;
    }

    public async Task<bool> ActivateRemoteAsync(string endpoint, string key, CancellationToken token = default)
    {
        var client = new RemoteLicenseClient();
        var license = await client.ActivateAsync(endpoint, key, HardwareId, token);
        return license is not null && Activate(license);
    }

    public async Task<bool> ValidateRemoteAsync(string endpoint, CancellationToken token = default)
    {
        if (Current is null || string.IsNullOrWhiteSpace(endpoint)) return false;
        var client = new RemoteLicenseClient();
        var license = await client.ValidateAsync(endpoint, Current.Key, HardwareId, token);
        if (license is null)
        {
            if (Current.IsActive) return true; // temporary server/network failure; local expiry still applies
            return false;
        }
        return Activate(license);
    }
}
