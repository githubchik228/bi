using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RustPerformanceSuite.OfflineLicensing;

public sealed record LicensePayload(
    string Key,
    string Plan,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    string? HardwareId);

public sealed record SignedLicense(LicensePayload Payload, string SignatureBase64)
{
    public static SignedLicense Parse(string json) =>
        JsonSerializer.Deserialize<SignedLicense>(json) ?? throw new InvalidDataException("Invalid license file.");

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class OfflineLicenseVerifier
{
    // Replace this public key once with the public key produced by the private generator key.
    // Never put the private signing key in the application or repository.
    private const string PublicKeyPem = "";

    public static bool Verify(SignedLicense license, string hardwareId, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(PublicKeyPem)) { error = "license_public_key_not_configured"; return false; }
        if (license.Payload.ExpiresAt is not null && license.Payload.ExpiresAt <= DateTimeOffset.UtcNow) { error = "expired"; return false; }
        if (!string.IsNullOrWhiteSpace(license.Payload.HardwareId) && !string.Equals(license.Payload.HardwareId, hardwareId, StringComparison.OrdinalIgnoreCase)) { error = "hardware_mismatch"; return false; }

        var data = CanonicalPayload(license.Payload);
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(PublicKeyPem);
            var signature = Convert.FromBase64String(license.SignatureBase64);
            if (!rsa.VerifyData(Encoding.UTF8.GetBytes(data), signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) { error = "invalid_signature"; return false; }
            return true;
        }
        catch (Exception ex) { error = ex is FormatException ? "invalid_signature_encoding" : "invalid_license"; return false; }
    }

    public static string CanonicalPayload(LicensePayload p) =>
        string.Join("|", p.Key.Trim().ToUpperInvariant(), p.Plan.Trim().ToLowerInvariant(), p.CreatedAt.ToUniversalTime().ToString("O"), p.ExpiresAt?.ToUniversalTime().ToString("O") ?? "", p.HardwareId?.Trim() ?? "");
}
