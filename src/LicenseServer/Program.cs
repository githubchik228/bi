using System.Security.Cryptography;
using System.Text.Json;
using LicenseServer;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var storePath = Path.Combine(AppContext.BaseDirectory, "licenses.json");
var licenses = Load();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/v1/license/activate", (ActivateRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.HardwareId)) return Results.BadRequest(new { error = "key_and_hwid_required" });
    if (!licenses.TryGetValue(request.Key.Trim().ToUpperInvariant(), out var license)) return Results.NotFound(new { error = "invalid_key" });
    if (license.Status != "Active") return Results.BadRequest(new { error = "key_disabled" });
    if (license.ExpiresAt is not null && license.ExpiresAt <= DateTimeOffset.UtcNow) return Results.BadRequest(new { error = "expired" });
    if (license.HardwareId is not null && !string.Equals(license.HardwareId, request.HardwareId.Trim(), StringComparison.OrdinalIgnoreCase)) return Results.BadRequest(new { error = "hardware_mismatch" });
    license.HardwareId ??= request.HardwareId.Trim(); license.ActivatedAt ??= DateTimeOffset.UtcNow; Save(); return Results.Ok(license);
});

app.MapPost("/v1/license/validate", (ValidateRequest request) =>
{
    if (!licenses.TryGetValue(request.Key.Trim().ToUpperInvariant(), out var license)) return Results.NotFound(new { error = "invalid_key" });
    var active = license.Status == "Active" && (license.ExpiresAt is null || license.ExpiresAt > DateTimeOffset.UtcNow);
    return Results.Ok(new { active, license });
});

app.MapPost("/v1/admin/keys", (CreateKeyRequest request, HttpRequest http) =>
{
    if (!Authorized(http)) return Results.Unauthorized();
    var record = Create(request.Plan);
    return record is null ? Results.BadRequest(new { error = "unsupported_plan" }) : Results.Ok(record);
});

app.MapGet("/v1/admin/keys", (HttpRequest http) => !Authorized(http) ? Results.Unauthorized() : Results.Ok(licenses.Values.OrderByDescending(x => x.CreatedAt)));
app.MapPost("/v1/admin/keys/{key}/revoke", (string key, HttpRequest http) => AdminAction(key, http, x => x.Status = "Revoked"));
app.MapPost("/v1/admin/keys/{key}/unbind", (string key, HttpRequest http) => AdminAction(key, http, x => { x.HardwareId = null; x.ActivatedAt = null; }));

AdminPanel.Map(app, authorization => Authorized(authorization), plan => Create(plan)?.Key, () => licenses.Values.OrderByDescending(x => x.CreatedAt).ToArray(), key => TryMutate(key, x => x.Status = "Revoked"), key => TryMutate(key, x => { x.HardwareId = null; x.ActivatedAt = null; }), (_, _) => false);

app.Run();

LicenseRecord? Create(string plan)
{
    plan = plan.Trim().ToLowerInvariant();
    if (plan is not ("1d" or "7d" or "30d" or "1y" or "lifetime")) return null;
    DateTimeOffset? expires = plan switch { "1d" => DateTimeOffset.UtcNow.AddDays(1), "7d" => DateTimeOffset.UtcNow.AddDays(7), "30d" => DateTimeOffset.UtcNow.AddDays(30), "1y" => DateTimeOffset.UtcNow.AddYears(1), _ => null };
    var key = GenerateKey(); var record = new LicenseRecord { Key = key, Plan = plan, Status = "Active", CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = expires }; licenses[key] = record; Save(); return record;
}

IResult AdminAction(string key, HttpRequest http, Action<LicenseRecord> action) => !Authorized(http) ? Results.Unauthorized() : TryMutate(key, action) ? Results.Ok(licenses[key.Trim().ToUpperInvariant()]) : Results.NotFound(new { error = "invalid_key" });
bool TryMutate(string key, Action<LicenseRecord> action) { var normalized = key.Trim().ToUpperInvariant(); if (!licenses.TryGetValue(normalized, out var l)) return false; action(l); Save(); return true; }

bool Authorized(HttpRequest request) => Authorized(request.Headers.Authorization.ToString());
bool Authorized(string authorization) { var expected = Environment.GetEnvironmentVariable("UNDOPTI_ADMIN_TOKEN"); return !string.IsNullOrWhiteSpace(expected) && authorization == $"Bearer {expected}"; }
Dictionary<string, LicenseRecord> Load() => File.Exists(storePath) ? JsonSerializer.Deserialize<Dictionary<string, LicenseRecord>>(File.ReadAllText(storePath)) ?? new() : new();
void Save() => File.WriteAllText(storePath, JsonSerializer.Serialize(licenses, new JsonSerializerOptions { WriteIndented = true }));
static string GenerateKey() => $"UND-{RandomNumberGenerator.GetHexString(4)}-{RandomNumberGenerator.GetHexString(4)}-{RandomNumberGenerator.GetHexString(4)}-{RandomNumberGenerator.GetHexString(4)}";
record ActivateRequest(string Key, string HardwareId);
record ValidateRequest(string Key, string HardwareId);
record CreateKeyRequest(string Plan);
sealed class LicenseRecord { public string Key { get; set; } = ""; public string Plan { get; set; } = ""; public string Status { get; set; } = "Active"; public DateTimeOffset CreatedAt { get; set; } public DateTimeOffset? ActivatedAt { get; set; } public DateTimeOffset? ExpiresAt { get; set; } public string? HardwareId { get; set; } }
