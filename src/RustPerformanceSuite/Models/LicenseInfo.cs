using System.Text.Json.Serialization;

namespace RustPerformanceSuite.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LicensePlan { Day, SevenDays, Month, Year, Lifetime }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LicenseRole { User, Helper, Admin, Owner }

public sealed class LicenseInfo
{
    public string Key { get; init; } = "";
    public LicensePlan Plan { get; init; }
    public LicenseRole Role { get; init; } = LicenseRole.User;
    public DateTimeOffset ActivatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public string HardwareId { get; init; } = "";
    [JsonIgnore] public bool IsActive => ExpiresAt is null || ExpiresAt > DateTimeOffset.UtcNow;
}