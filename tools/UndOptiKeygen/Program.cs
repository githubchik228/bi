using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
const string appName = "UndOpti";
var keyDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    appName,
    "Licensing");
Directory.CreateDirectory(keyDirectory);
var privateKeyPath = Path.Combine(
    keyDirectory,
    "undopti-signing-private.pem");
using var rsa = RSA.Create(3072);
if (File.Exists(privateKeyPath))
{
    rsa.ImportFromPem(File.ReadAllText(privateKeyPath));
}
else
{
    File.WriteAllText(
        privateKeyPath,
        rsa.ExportRSAPrivateKeyPem(),
        new UTF8Encoding(false));
    File.SetAttributes(privateKeyPath, FileAttributes.Hidden);
    Console.WriteLine("NEW SIGNING KEY CREATED");
    Console.WriteLine();
    Console.WriteLine("PRIVATE KEY:");
    Console.WriteLine(privateKeyPath);
    Console.WriteLine();
    Console.WriteLine("KEEP THIS FILE PRIVATE!");
    Console.WriteLine();
    Console.WriteLine("PUBLIC KEY:");
    Console.WriteLine(rsa.ExportSubjectPublicKeyInfoPem());
    Console.WriteLine();
}
Console.WriteLine("========================================");
Console.WriteLine("        UNDOPTI KEY GENERATOR");
Console.WriteLine("========================================");
Console.WriteLine();
Console.Write("Role (USER/HELPER/ADMIN/OWNER): ");
var role = (Console.ReadLine() ?? "USER")
    .Trim()
    .ToUpperInvariant();
if (role is not ("USER" or "HELPER" or "ADMIN" or "OWNER"))
{
    Console.WriteLine("Invalid role.");
    return;
}
string plan;
if (role == "OWNER")
{
    plan = "lifetime";
    Console.WriteLine("OWNER automatically uses Lifetime.");
}
else
{
    Console.Write("Plan (1d/7d/30d/1y/lifetime): ");
    plan = (Console.ReadLine() ?? "")
        .Trim()
        .ToLowerInvariant();
    if (plan is not ("1d" or "7d" or "30d" or "1y" or "lifetime"))
    {
        Console.WriteLine("Invalid plan.");
        return;
    }
}
Console.Write("HWID (leave empty for first activation): ");
var hwid = (Console.ReadLine() ?? "").Trim();
var createdAt = DateTimeOffset.UtcNow;
DateTimeOffset? expiresAt = plan switch
{
    "1d" => createdAt.AddDays(1),
    "7d" => createdAt.AddDays(7),
    "30d" => createdAt.AddDays(30),
    "1y" => createdAt.AddYears(1),
    _ => null
};
var key =
    $"UND-{RandomNumberGenerator.GetHexString(4)}-" +
    $"{RandomNumberGenerator.GetHexString(4)}-" +
    $"{RandomNumberGenerator.GetHexString(4)}-" +
    $"{RandomNumberGenerator.GetHexString(4)}";
var payload = new LicensePayload
{
    Key = key,
    Role = role,
    Plan = plan,
    CreatedAt = createdAt,
    ExpiresAt = expiresAt,
    HardwareId = string.IsNullOrWhiteSpace(hwid)
        ? null
        : hwid
};
var canonical = string.Join(
    "|",
    payload.Key.ToUpperInvariant(),
    payload.Role.ToUpperInvariant(),
    payload.Plan.ToLowerInvariant(),
    payload.CreatedAt.ToUniversalTime().ToString("O"),
    payload.ExpiresAt?.ToUniversalTime().ToString("O") ?? "",
    payload.HardwareId ?? "");
var signature = rsa.SignData(
    Encoding.UTF8.GetBytes(canonical),
    HashAlgorithmName.SHA256,
    RSASignaturePadding.Pss);
var license = new SignedLicense
{
    Payload = payload,
    SignatureBase64 = Convert.ToBase64String(signature)
};
var outputDirectory = Path.Combine(
    AppContext.BaseDirectory,
    "licenses");
Directory.CreateDirectory(outputDirectory);
var outputFile = Path.Combine(
    outputDirectory,
    $"{key}.license.json");
File.WriteAllText(
    outputFile,
    JsonSerializer.Serialize(
        license,
        new JsonSerializerOptions
        {
            WriteIndented = true
        }),
    new UTF8Encoding(false));
Console.WriteLine();
Console.WriteLine("========================================");
Console.WriteLine(" LICENSE CREATED");
Console.WriteLine("========================================");
Console.WriteLine($"Key:     {key}");
Console.WriteLine($"Role:    {role}");
Console.WriteLine($"Plan:    {plan}");
Console.WriteLine(
    $"Expires: {(expiresAt.HasValue
        ? expiresAt.Value.ToLocalTime().ToString()
        : "Never")}");
Console.WriteLine();
Console.WriteLine($"File: {outputFile}");
Console.WriteLine();
Console.ReadLine();
public sealed class LicensePayload
{
    public string Key { get; set; } = "";
    public string Role { get; set; } = "";
    public string Plan { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? HardwareId { get; set; }
}
public sealed class SignedLicense
{
    public LicensePayload Payload { get; set; } = new();
    public string SignatureBase64 { get; set; } = "";
}
