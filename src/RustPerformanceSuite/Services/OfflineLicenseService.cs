using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace RustPerformanceSuite.Services;
public sealed class OfflineLicenseService
{
    private const string PublicKeyPem = """
-----BEGIN PUBLIC KEY-----
PASTE_YOUR_PUBLIC_KEY_HERE
-----END PUBLIC KEY-----
""";
    public LicenseResult Validate(string licenseFile, string? currentHwid = null)
    {
        if (!File.Exists(licenseFile))
            return LicenseResult.Fail("License file not found.");
        try
        {
            var json = File.ReadAllText(licenseFile);
            var license = JsonSerializer.Deserialize<SignedLicense>(json);
            if (license?.Payload == null ||
                string.IsNullOrWhiteSpace(license.SignatureBase64))
                return LicenseResult.Fail("Invalid license format.");
            var payload = license.Payload;
            var canonical = string.Join(
                "|",
                payload.Key.ToUpperInvariant(),
                payload.Role.ToUpperInvariant(),
                payload.Plan.ToLowerInvariant(),
                payload.CreatedAt.ToUniversalTime().ToString("O"),
                payload.ExpiresAt?.ToUniversalTime().ToString("O") ?? "",
                payload.HardwareId ?? "");
            using var rsa = RSA.Create();
            rsa.ImportFromPem(PublicKeyPem);
            var validSignature = rsa.VerifyData(
                Encoding.UTF8.GetBytes(canonical),
                Convert.FromBase64String(license.SignatureBase64),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pss);
            if (!validSignature)
                return LicenseResult.Fail("Invalid license signature.");
            if (payload.ExpiresAt.HasValue &&
                DateTimeOffset.UtcNow >= payload.ExpiresAt.Value)
            {
                return LicenseResult.Fail("License expired.");
            }
            if (!string.IsNullOrWhiteSpace(payload.HardwareId) &&
                !string.Equals(
                    payload.HardwareId,
                    currentHwid,
                    StringComparison.OrdinalIgnoreCase))
            {
                return LicenseResult.Fail("License is bound to another device.");
            }
            return LicenseResult.Success(payload);
        }
        catch
        {
            return LicenseResult.Fail("License validation failed.");
        }
    }
    public static string GetCurrentHwid()
    {
        var machine = Environment.MachineName;
        var user = Environment.UserName;
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(
            $"{machine}|{user}");
        return Convert.ToHexString(
            sha.ComputeHash(bytes));
    }
}
public sealed class SignedLicense
{
    public LicensePayload Payload { get; set; } = new();
    public string SignatureBase64 { get; set; } = "";
}
public sealed class LicensePayload
{
    public string Key { get; set; } = "";
    public string Role { get; set; } = "";
    public string Plan { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? HardwareId { get; set; }
}
public sealed class LicenseResult
{
    public bool IsValid { get; private init; }
    public string Message { get; private init; } = "";
    public LicensePayload? License { get; private init; }
    public static LicenseResult Success(LicensePayload license) =>
        new()
        {
            IsValid = true,
            Message = "License activated.",
            License = license
        };
    public static LicenseResult Fail(string message) =>
        new()
        {
            IsValid = false,
            Message = message
        };
}
