using System.Text.Json;
using RustPerformanceSuite.Models;

namespace RustPerformanceSuite.Services;

public sealed class LicenseService
{
    private readonly string _file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UndOpti", "license.json");
    private readonly HardwareIdService _hardwareId = new();
    private readonly OfflineLicenseVerifier _offlineVerifier = new();
    public LicenseInfo? Current { get; private set; }
    public string HardwareId => _hardwareId.GetHardwareId();
    public bool IsLicensed => Current?.IsActive == true && Current.HardwareId.Equals(HardwareId, StringComparison.OrdinalIgnoreCase);
    public bool IsExpired => Current is not null && !Current.IsActive;
    public bool HasExpired => IsExpired;
    public string PublicKeyPath => _offlineVerifier.PublicKeyPath;

    public LicenseService() => Load();

    public void Load()
    {
        try
        {
            if (!File.Exists(_file)) return;
            Current = JsonSerializer.Deserialize<LicenseInfo>(File.ReadAllText(_file));
        }
        catch { Current = null; }
    }

    public bool Activate(LicenseInfo license)
    {
        if (string.IsNullOrWhiteSpace(license.Key) ||
            !license.HardwareId.Equals(HardwareId, StringComparison.OrdinalIgnoreCase) ||
            !license.IsActive) return false;

        Current = license;
        SaveCurrent();
        return true;
    }

    public bool ActivateOfflineLicense(string licensePath, out string error)
    {
        error = "";
        if (!File.Exists(licensePath)) { error = "License file not found."; return false; }

        var json = File.ReadAllText(licensePath);
        if (!_offlineVerifier.Verify(json, HardwareId, out var payload, out error) || payload is null)
            return false;

        var plan = payload.Plan.Trim().ToLowerInvariant() switch
        {
            "1d" => LicensePlan.Day,
            "7d" => LicensePlan.SevenDays,
            "30d" => LicensePlan.Month,
            "1y" => LicensePlan.Year,
            "lifetime" => LicensePlan.Lifetime,
            _ => throw new InvalidOperationException("Unknown license plan.")
        };

        // A license generated without a HWID becomes bound to this device locally.
        // The original signed file remains untouched, so its signature stays valid.
        Current = new LicenseInfo
        {
            Key = payload.Key,
            Plan = plan,
            Role = LicenseRole.User,
            ActivatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = payload.ExpiresAt,
            HardwareId = HardwareId
        };
        SaveCurrent();
        return true;
    }

    private void SaveCurrent()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
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
        if (license is null) return Current.IsActive;
        return Activate(license);
    }
}
