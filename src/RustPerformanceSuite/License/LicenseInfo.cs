namespace RustPerformanceSuite.License;

public sealed record LicenseInfo(
    string Key,
    string Plan,
    DateTime ActivatedAtUtc,
    DateTime ExpiresAtUtc,
    string HardwareId)
{
    public bool IsLifetime => ExpiresAtUtc == DateTime.MaxValue;
    public bool IsActive => IsLifetime || DateTime.UtcNow < ExpiresAtUtc;
}
