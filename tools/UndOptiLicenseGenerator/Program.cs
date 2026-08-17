using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

Console.WriteLine("UndOpti Offline License Generator");
Console.WriteLine("Plans: 1d, 7d, 30d, 1y, lifetime");
Console.Write("Plan: ");
var plan = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
if (plan is not ("1d" or "7d" or "30d" or "1y" or "lifetime")) { Console.WriteLine("Invalid plan."); return; }

Console.Write("HWID (leave empty for first-run binding): ");
var hwid = (Console.ReadLine() ?? "").Trim();
var created = DateTimeOffset.UtcNow;
DateTimeOffset? expires = plan switch
{
    "1d" => created.AddDays(1),
    "7d" => created.AddDays(7),
    "30d" => created.AddDays(30),
    "1y" => created.AddYears(1),
    _ => null
};

var payload = new LicensePayload($"UND-{RandomNumberGenerator.GetHexString(4)}-{RandomNumberGenerator.GetHexString(4)}-{RandomNumberGenerator.GetHexString(4)}", plan, created, expires, string.IsNullOrWhiteSpace(hwid) ? null : hwid);
var json = JsonSerializer.Serialize(payload);
var licenseDir = Path.Combine(AppContext.BaseDirectory, "licenses");
Directory.CreateDirectory(licenseDir);
var file = Path.Combine(licenseDir, payload.Key + ".license.json");
File.WriteAllText(file, json, Encoding.UTF8);
Console.WriteLine($"Created: {file}");
Console.WriteLine("IMPORTANT: This bootstrap generator creates the license payload. Production signing should use a private key kept outside the repository.");

record LicensePayload(string Key, string Plan, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, string? HardwareId);
