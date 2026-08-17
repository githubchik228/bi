using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<LicenseStore>();
var app = builder.Build();

app.MapPost("/api/license/activate", (ActivateRequest request, LicenseStore store) =>
{
    var result = store.Activate(request.Key, request.HardwareId);
    return result is null ? Results.Unauthorized() : Results.Ok(result);
});

app.MapPost("/api/license/validate", (ValidateRequest request, LicenseStore store) =>
{
    var result = store.Validate(request.Key, request.HardwareId);
    return result is null ? Results.Unauthorized() : Results.Ok(result);
});

app.MapGet("/health", () => Results.Ok(new { service = "UndOpti License Server", status = "ok" }));
app.Run();

public sealed record ActivateRequest(string Key, string HardwareId);
public sealed record ValidateRequest(string Key, string HardwareId);
public sealed record LicenseResponse(string Key, string Plan, DateTime ActivatedAtUtc, DateTime ExpiresAtUtc, string HardwareId);

public sealed class LicenseStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, StoredLicense> _licenses = new(StringComparer.OrdinalIgnoreCase);

    public LicenseStore(IConfiguration configuration)
    {
        var configured = configuration.GetSection("Licenses").GetChildren();
        foreach (var item in configured)
        {
            var key = item["Key"];
            var plan = item["Plan"];
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(plan))
                _licenses[key] = new StoredLicense(key, plan);
        }
    }

    public LicenseResponse? Activate(string key, string hardwareId)
    {
        lock (_gate)
        {
            if (!_licenses.TryGetValue(key.Trim(), out var license) || string.IsNullOrWhiteSpace(hardwareId)) return null;
            if (license.HardwareId is not null && !license.HardwareId.Equals(hardwareId, StringComparison.OrdinalIgnoreCase)) return null;
            if (license.ExpiresAtUtc <= DateTime.UtcNow) return null;
            license.HardwareId ??= hardwareId;
            license.ActivatedAtUtc ??= DateTime.UtcNow;
            return ToResponse(license);
        }
    }

    public LicenseResponse? Validate(string key, string hardwareId)
    {
        lock (_gate)
        {
            if (!_licenses.TryGetValue(key.Trim(), out var license) || license.HardwareId != hardwareId || license.ExpiresAtUtc <= DateTime.UtcNow) return null;
            return ToResponse(license);
        }
    }

    private static LicenseResponse ToResponse(StoredLicense x) => new(x.Key, x.Plan, x.ActivatedAtUtc!.Value, x.ExpiresAtUtc, x.HardwareId!);

    private sealed class StoredLicense
    {
        public StoredLicense(string key, string plan)
        {
            Key = key; Plan = plan;
            ExpiresAtUtc = plan.ToLowerInvariant() switch
            {
                "1d" => DateTime.UtcNow.AddDays(1),
                "7d" => DateTime.UtcNow.AddDays(7),
                "30d" => DateTime.UtcNow.AddDays(30),
                "1y" => DateTime.UtcNow.AddYears(1),
                "lifetime" => DateTime.MaxValue,
                _ => DateTime.MinValue
            };
        }
        public string Key { get; }
        public string Plan { get; }
        public string? HardwareId { get; set; }
        public DateTime? ActivatedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; }
    }
}
