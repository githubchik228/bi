using System.Security.Cryptography;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var storePath = Path.Combine(AppContext.BaseDirectory, "licenses.json");
var licenses = Load();

app.MapPost("/v1/license/activate", (ActivateRequest request) =>
{
    var hwid = request.HardwareId?.Trim();
    if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(hwid)) return Results.BadRequest(new { error = "key_and_hwid_required" });
    if (!licenses.TryGetValue(request.Key.Trim().ToUpperInvariant(), out var license)) return Results.NotFound(new { error = "invalid_key" });
    if (license.ExpiresAt is not null && license.ExpiresAt <= DateTimeOffset.UtcNow) return Results.BadRequest(new { error = "expired" });
    if (license.HardwareId is not null && !String.Equals(license.HardwareId, hwid, StringComparison.OrdinalIgnoreCase)) return Results.BadRequest(new { error = "hardware_mismatch" });
    license.HardwareId ??= hwid;
    license.ActivatedAt ??= DateTimeOffset.UtcNow;
    Save();
    return Results.Ok(license);
});

app.MapPost("/v1/license/validate", (ValidateRequest request) =>
{
    if (!licenses.TryGetValue(request.Key.Trim().ToUpperInvariant(), out var license)) return Results.NotFound(new { error = "invalid_key" });
    var active = license.ExpiresAt is null || license.ExpiresAt > DateTimeOffset.UtcNow;
    return Results.Ok(new { active, license });
});

app.MapPost("/v1/admin/keys", (CreateKeyRequest request, HttpRequest http) =>
{
    var expected = Environment.GetEnvironmentVariable("RPS_ADMIN_TOKEN");
    if (string.IsNullOrWhiteSpace(expected) || http.Headers.Authorization != $"Bearer {expected}") return Results.Unauthorized();
    var key = GenerateKey();
    DateTimeOffset? expires = request.Plan.ToLowerInvariant() switch
    {
        "1d" => DateTimeOffset.UtcNow.AddDays(1), "7d" => DateTimeOffset.UtcNow.AddDays(7), "30d" => DateTimeOffset.UtcNow.AddDays(30), "1y" => DateTimeOffset.UtcNow.AddYears(1), "lifetime" => null, _ => null
    };
    licenses[key] = new LicenseRecord { Key = key, Plan = request.Plan, ExpiresAt = expires, Role = "User" };
    Save();
    return Results.Ok(licenses[key]);
});

app.Run();

Dictionary<string, LicenseRecord> Load() => File.Exists(storePath) ? JsonSerializer.Deserialize<Dictionary<string, LicenseRecord>>(File.ReadAllText(storePath)) ?? new() : new();
void Save() => File.WriteAllText(storePath, JsonSerializer.Serialize(licenses, new JsonSerializerOptions { WriteIndented = true }));
static string GenerateKey() => $"RPS-{RandomNumberGenerator.GetHexString(4)}-{RandomNumberGenerator.GetHexString(4)}-{RandomNumberGenerator.GetHexString(4)}-{RandomNumberGenerator.GetHexString(4)}";
record ActivateRequest(string Key, string HardwareId);
record ValidateRequest(string Key);
record CreateKeyRequest(string Plan);
sealed class LicenseRecord { public string Key { get; set; } = ""; public string Plan { get; set; } = ""; public string Role { get; set; } = "User"; public DateTimeOffset? ActivatedAt { get; set; } public DateTimeOffset? ExpiresAt { get; set; } public string? HardwareId { get; set; } }