using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

const string privateKeyFileName = "undopti-signing-private.pem";
var keyDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UndOpti", "Licensing");
Directory.CreateDirectory(keyDir);
var privateKeyPath = Path.Combine(keyDir, privateKeyFileName);

using var rsa = RSA.Create(3072);
if (File.Exists(privateKeyPath))
    rsa.ImportFromPem(File.ReadAllText(privateKeyPath));
else
{
    File.WriteAllText(privateKeyPath, rsa.ExportRSAPrivateKeyPem(), new UTF8Encoding(false));
    File.SetAttributes(privateKeyPath, FileAttributes.Hidden);
    Console.WriteLine($"Created private signing key: {privateKeyPath}");
    Console.WriteLine("KEEP THIS FILE PRIVATE. Anyone with it can create valid licenses.");
    Console.WriteLine();
    Console.WriteLine("PUBLIC KEY — copy it to the client's OfflineLicenseVerifier configuration:");
    Console.WriteLine(rsa.ExportSubjectPublicKeyInfoPem());
}

Console.WriteLine("\nUndOpti Offline License Generator");
Console.Write("Role (USER/HELPER/ADMIN/OWNER): ");
var role = (Console.ReadLine() ?? "USER").Trim().ToUpperInvariant();
if (role is not ("USER" or "HELPER" or "ADMIN" or "OWNER")) { Console.WriteLine("Invalid role."); return; }

string plan;
if (role == "OWNER")
{
    plan = "lifetime";
    Console.WriteLine("OWNER license is always Lifetime.");
}
else
{
    Console.Write("Plan (1d/7d/30d/1y/lifetime): ");
    plan = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
    if (plan is not ("1d" or "7d" or "30d" or "1y" or "lifetime")) { Console.WriteLine("Invalid plan."); return; }
}

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

var payload = new LicensePayload(
    $"UND-{RandomNumberGenerator.GetHexString(4)}-{RandomNumberGenerator.GetHexString(4)}-{RandomNumberGenerator.GetHexString(4)}",
    plan, role, created, expires, string.IsNullOrWhiteSpace(hwid) ? null : hwid);

var canonical = string.Join("|",
    payload.Key.Trim().ToUpperInvariant(),
    payload.Plan.Trim().ToLowerInvariant(),
    payload.Role.Trim().ToUpperInvariant(),
    payload.CreatedAt.ToUniversalTime().ToString("O"),
    payload.ExpiresAt?.ToUniversalTime().ToString("O") ?? "",
    payload.HardwareId?.Trim() ?? "");

var signature = rsa.SignData(Encoding.UTF8.GetBytes(canonical), HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
var license = new SignedLicense(payload, Convert.ToBase64String(signature));
var licenseDir = Path.Combine(AppContext.BaseDirectory, "licenses");
Directory.CreateDirectory(licenseDir);
var file = Path.Combine(licenseDir, payload.Key + ".license.json");
File.WriteAllText(file, license.ToJson(), new UTF8Encoding(false));
Console.WriteLine($"\nCreated {role} license: {file}");
Console.WriteLine($"Key: {payload.Key}");
Console.WriteLine($"Plan: {payload.Plan}");
Console.WriteLine($"Role: {payload.Role}");
Console.WriteLine($"Expires: {payload.ExpiresAt?.ToLocalTime().ToString() ?? "Never"}");

record LicensePayload(string Key, string Plan, string Role, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, string? HardwareId);
record SignedLicense(LicensePayload Payload, string SignatureBase64)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}
