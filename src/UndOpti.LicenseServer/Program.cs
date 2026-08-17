using System.Security.Cryptography;

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

app.MapGet("/api/admin/keys", (HttpRequest request, LicenseStore store) =>
    !store.IsAdmin(request) ? Results.Unauthorized() : Results.Ok(store.List()));

app.MapPost("/api/admin/keys", (HttpRequest request, CreateKeyRequest body, LicenseStore store) =>
{
    if (!store.IsAdmin(request)) return Results.Unauthorized();
    var result = store.Create(body.Plan, body.Role);
    return result is null ? Results.BadRequest("Unsupported plan") : Results.Ok(result);
});

app.MapPost("/api/admin/keys/revoke", (HttpRequest request, RevokeKeyRequest body, LicenseStore store) =>
{
    if (!store.IsAdmin(request)) return Results.Unauthorized();
    return store.Revoke(body.Key) ? Results.Ok() : Results.NotFound();
});

app.Run();

public sealed record ActivateRequest(string Key, string HardwareId);
public sealed record ValidateRequest(string Key, string HardwareId);
public sealed record CreateKeyRequest(string Plan, string? Role = null);
public sealed record RevokeKeyRequest(string Key);
public sealed record LicenseResponse(string Key, string Plan, LicenseRoleDto Role, DateTimeOffset ActivatedAt, DateTimeOffset? ExpiresAt, string HardwareId);
public sealed record AdminKeyView(string Key, string Plan, LicenseRoleDto Role, string? HardwareId, DateTimeOffset? ActivatedAt, DateTimeOffset? ExpiresAt, bool Revoked);
public enum LicenseRoleDto { User, Helper, Admin, Owner }

public sealed class LicenseStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, StoredLicense> _licenses = new(StringComparer.OrdinalIgnoreCase);
    private readonly IConfiguration _configuration;

    public LicenseStore(IConfiguration configuration)
    {
        _configuration = configuration;
        foreach (var item in configuration.GetSection("Licenses").GetChildren())
        {
            var key = item["Key"]; var plan = item["Plan"];
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(plan))
                _licenses[key] = new StoredLicense(key, plan, ParseRole(item["Role"]));
        }
    }

    public bool IsAdmin(HttpRequest request) => request.Headers.TryGetValue("X-UndOpti-Admin-Key", out var supplied) &&
        !string.IsNullOrWhiteSpace(_configuration["AdminApiKey"]) && supplied.ToString() == _configuration["AdminApiKey"];

    public LicenseResponse? Activate(string key, string hardwareId)
    {
        lock (_gate)
        {
            if (!_licenses.TryGetValue(key.Trim(), out var license) || license.Revoked || string.IsNullOrWhiteSpace(hardwareId)) return null;
            if (license.HardwareId is not null && !license.HardwareId.Equals(hardwareId, StringComparison.OrdinalIgnoreCase)) return null;
            var now = DateTimeOffset.UtcNow;
            if (license.ExpiresAt.HasValue && license.ExpiresAt <= now) return null;
            license.HardwareId ??= hardwareId;
            license.ActivatedAt ??= now;
            if (!license.ExpiresAt.HasValue && !license.IsLifetime) license.ExpiresAt = license.ActivatedAt.Value.Add(license.Duration);
            return ToResponse(license);
        }
    }

    public LicenseResponse? Validate(string key, string hardwareId)
    {
        lock (_gate)
        {
            if (!_licenses.TryGetValue(key.Trim(), out var license) || license.Revoked || license.HardwareId is null || !license.HardwareId.Equals(hardwareId, StringComparison.OrdinalIgnoreCase)) return null;
            if (license.ExpiresAt.HasValue && license.ExpiresAt <= DateTimeOffset.UtcNow) return null;
            return ToResponse(license);
        }
    }

    public IEnumerable<AdminKeyView> List() => _licenses.Values.Select(x => new AdminKeyView(x.Key, x.Plan, x.Role, x.HardwareId, x.ActivatedAt, x.ExpiresAt, x.Revoked));

    public AdminKeyView? Create(string plan, string? role)
    {
        var normalized = plan.Trim().ToLowerInvariant();
        if (!new[] { "1d", "7d", "30d", "1y", "lifetime" }.Contains(normalized)) return null;
        var key = $"UNDOPTI-{RandomToken(5)}-{RandomToken(5)}";
        var item = new StoredLicense(key, normalized, ParseRole(role));
        lock (_gate) _licenses[key] = item;
        return new AdminKeyView(item.Key, item.Plan, item.Role, null, null, item.ExpiresAt, false);
    }

    public bool Revoke(string key) { lock (_gate) return _licenses.TryGetValue(key.Trim(), out var item) && (item.Revoked = true); }

    private static string RandomToken(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(length);
        return string.Concat(bytes.Select(b => chars[b % chars.Length]));
    }

    private static LicenseResponse ToResponse(StoredLicense x) => new(x.Key, ToPlan(x.Plan), x.Role, x.ActivatedAt!.Value, x.ExpiresAt, x.HardwareId!);
    private static string ToPlan(string plan) => plan switch { "1d" => "Day", "7d" => "SevenDays", "30d" => "Month", "1y" => "Year", "lifetime" => "Lifetime", _ => "Day" };
    private static LicenseRoleDto ParseRole(string? role) => Enum.TryParse<LicenseRoleDto>(role, true, out var value) ? value : LicenseRoleDto.User;

    private sealed class StoredLicense
    {
        public StoredLicense(string key, string plan, LicenseRoleDto role) { Key = key; Plan = plan.ToLowerInvariant(); Role = role; Duration = Plan switch { "1d" => TimeSpan.FromDays(1), "7d" => TimeSpan.FromDays(7), "30d" => TimeSpan.FromDays(30), "1y" => TimeSpan.FromDays(365), "lifetime" => TimeSpan.Zero, _ => TimeSpan.MinValue }; IsLifetime = Plan == "lifetime"; }
        public string Key { get; }
        public string Plan { get; }
        public LicenseRoleDto Role { get; }
        public TimeSpan Duration { get; }
        public bool IsLifetime { get; }
        public string? HardwareId { get; set; }
        public DateTimeOffset? ActivatedAt { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public bool Revoked { get; set; }
    }
}
