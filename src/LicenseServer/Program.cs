using System.Security.Cryptography;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var storePath = Path.Combine(AppContext.BaseDirectory, "licenses.json");
var licenses = Load();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/v1/license/activate", (ActivateRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.HardwareId))
        return Results.BadRequest(new { error = "key_and_hwid_required" });

    if (!licenses.TryGetValue(request.Key.Trim().ToUpperInvariant(), out var license))
        return Results.NotFound(new { error = "invalid_key" });

    if (license.Status != "Active") return Results.BadRequest(new { error = "key_disabled" });
    if (license.ExpiresAt is not null && license.ExpiresAt <= DateTimeOffset.UtcNow)
        return Results.BadRequest(new { error = "expired" });
    if (license.HardwareId is not null && !string.Equals(license.HardwareId, request.HardwareId.Trim(), StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "hardware_mismatch" });

    license.HardwareId ??= request.HardwareId.Trim();
    license.ActivatedAt ??= DateTimeOffset.UtcNow;
    Save();
    return Results.Ok(license);
});

app.MapPost("/v1/license/validate", (ValidateRequest request) =>
{
    if (!licenses.TryGetValue(request.Key.Trim().ToUpperInvariant(), out var license))
        return Results.NotFound(new { error = "invalid_key" });

    var active = license.Status == "Active" && (license.ExpiresAt is null || license.ExpiresAt > DateTimeOffset.UtcNow);
    return Results.Ok(new { active, license });
});

app.MapPost("/v1/admin/keys", (CreateKeyRequest request, HttpRequest http) =>
{
    if (!Authorized(http)) return Results.Unauthorized();
    var plan = request.Plan.Trim().ToLowerInvariant();
    if (plan is not ("1d" or "7d" or "30d" or "1y" or "lifetime"))
        return Results.BadRequest(new { error = "unsupported_plan" });

    var key = GenerateKey();
    DateTimeOffset? expires = plan switch
    {
        "1d" => DateTimeOffset.UtcNow.AddDays(1),
        "7d" => DateTimeOffset.UtcNow.AddDays(7),
        "30d" => DateTimeOffset.UtcNow.AddDays(30),
        "1y" => DateTimeOffset.UtcNow.AddYears(1),
        _ => null
    };

    var record = new LicenseRecord
    {
        Key = key,
        Plan = plan,
        Status = "Active",
        CreatedAt = DateTimeOffset.UtcNow,
        ExpiresAt = expires
    };
    licenses[key] = record;
    Save();
    return Results.Ok(record);
});

app.MapGet("/v1/admin/keys", (HttpRequest http) =>
{
    if (!Authorized(http)) return Results.Unauthorized();
    return Results.Ok(licenses.Values.OrderByDescending(x => x.CreatedAt));
});

app.MapPost("/v1/admin/keys/{key}/revoke", (string key, HttpRequest http) =>
{
    if (!Authorized(http)) return Results.Unauthorized();
    var normalized = key.Trim().ToUpperInvariant();
    if (!licenses.TryGetValue(normalized, out var license))
        return Results.NotFound(new { error = "invalid_key" });

    license.Status = "Revoked";
    Save();
    return Results.Ok(license);
});

app.MapPost("/v1/admin/keys/{key}/unbind", (string key, HttpRequest http) =>
{
    if (!Authorized(http)) return Results.Unauthorized();
    var normalized = key.Trim().ToUpperInvariant();
    if (!licenses.TryGetValue(normalized, out var license))
        return Results.NotFound(new { error = "invalid_key" });

    license.HardwareId = null;
    license.ActivatedAt = null;
    Save();
    return Results.Ok(license);
});

app.Run();

bool Authorized(HttpRequest request)
{
    var expected = Environment.GetEnvironmentVariable("UNDOPTI_ADMIN_TOKEN");
    return !string.IsNullOrWhiteSpace(expected)
        && request.Headers.Authorization == $"Bearer {expected}";
}

Dictionary<string, LicenseRecord> Load() =>
    File.Exists(storePath)
        ? JsonSerializer.Deserialize<Dictionary<string, LicenseRecord>>(File.ReadAllText(storePath)) ?? new()
        : new();

void Save() => File.WriteAllText(storePath, JsonSerializer.Serialize(licenses, new JsonSerializerOptions { WriteIndented = true }));

static string GenerateKey() => $"UND-{RandomNumberGenerator.GetHexString(4)}-{RandomNumberGenerator.GetHexString(4)}-{RandomNumberGenerator.GetHexString(4)}-{RandomNumberGenerator.GetHexString(4)}";

record ActivateRequest(string Key, string HardwareId);
record ValidateRequest(string Key, string HardwareId);
record CreateKeyRequest(string Plan);

sealed class LicenseRecord
{
    public string Key { get; set; } = "";
    public string Plan { get; set; } = "";
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? HardwareId { get; set; }
}
