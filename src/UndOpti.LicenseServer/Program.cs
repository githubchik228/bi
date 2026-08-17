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
public sealed record LicenseResponse(string Key, string Plan, DateTimeOffset ActivatedAt, DateTimeOffset? ExpiresAt, string HardwareId);

public sealed class LicenseStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, StoredLicense> _licenses = new(StringComparer.OrdinalIgnoreCase);

    public LicenseStore(IConfiguration configuration)
    {
        foreach (var item in configuration.GetSection("Licenses").GetChildren())
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
            if (!_licenses.TryGetValue(key.Trim(), out var license) || string.IsNullOrWhiteSpace(hardwareId) || license.Revoked)
                return null;
            if (license.HardwareId is not null && !license.HardwareId.Equals(hardwareId, StringComparison.OrdinalIgnoreCase)) return null;

            var now = DateTimeOffset.UtcNow;
            if (license.ExpiresAt.HasValue && license.ExpiresAt <= now) return null;
            license.HardwareId ??= hardwareId;
            license.ActivatedAt ??= now;
            if (!license.ExpiresAt.HasValue && !license.IsLifetime)
                license.ExpiresAt = license.ActivatedAt.Value.Add(license.Duration);
            return ToResponse(license);
        }
    }

    public LicenseResponse? Validate(string key, string hardwareId)
    {
        lock (_gate)
        {
            if (!_licenses.TryGetValue(key.Trim(), out var license) || license.Revoked || license.HardwareId is null ||
                !license.HardwareId.Equals(hardwareId, StringComparison.OrdinalIgnoreCase)) return null;
            if (license.ExpiresAt.HasValue && license.ExpiresAt <= DateTimeOffset.UtcNow) return null;
            return ToResponse(license);
        }
    }

    private static LicenseResponse ToResponse(StoredLicense x) =>
        new(x.Key, x.Plan, x.ActivatedAt!.Value, x.ExpiresAt, x.HardwareId!);

    private sealed class StoredLicense
    {
        public StoredLicense(string key, string plan)
        {
            Key = key;
            Plan = plan.ToLowerInvariant();
            Duration = Plan switch
            {
                "1d" => TimeSpan.FromDays(1),
                "7d" => TimeSpan.FromDays(7),
                "30d" => TimeSpan.FromDays(30),
                "1y" => TimeSpan.FromDays(365),
                "lifetime" => TimeSpan.Zero,
                _ => TimeSpan.MinValue
            };
            IsLifetime = Plan == "lifetime";
        }

        public string Key { get; }
        public string Plan { get; }
        public TimeSpan Duration { get; }
        public bool IsLifetime { get; }
        public string? HardwareId { get; set; }
        public DateTimeOffset? ActivatedAt { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public bool Revoked { get; set; }
    }
}
