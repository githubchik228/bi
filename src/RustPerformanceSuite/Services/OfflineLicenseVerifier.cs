using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RustPerformanceSuite.Services;

public sealed class OfflineLicenseVerifier
{
    private readonly string _publicKeyPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UndOpti", "Licensing", "undopti-public.pem");

    public bool Verify(string licenseJson, string hardwareId, out OfflineLicensePayload? payload, out string error)
    {
        payload = null;
        error = "";
        try
        {
            var signed = JsonSerializer.Deserialize<SignedLicense>(licenseJson);
            if (signed?.Payload is null || string.IsNullOrWhiteSpace(signed.SignatureBase64))
            {
                error = "Invalid license format.";
                return false;
            }

            if (!File.Exists(_publicKeyPath))
            {
                error = $"UndOpti public signing key is missing: {_publicKeyPath}";
                return false;
            }

            var p = signed.Payload;
            var canonical = string.Join("|",
                p.Key.Trim().ToUpperInvariant(),
                p.Plan.Trim().ToLowerInvariant(),
                p.CreatedAt.ToUniversalTime().ToString("O"),
                p.ExpiresAt?.ToUniversalTime().ToString("O") ?? "",
                p.HardwareId?.Trim() ?? "");

            using var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(_publicKeyPath));
            var signature = Convert.FromBase64String(signed.SignatureBase64);
            if (!rsa.VerifyData(Encoding.UTF8.GetBytes(canonical), signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            {
                error = "License signature is invalid.";
                return false;
            }

            if (p.ExpiresAt is not null && p.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                error = "License has expired.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(p.HardwareId) && !p.HardwareId.Equals(hardwareId, StringComparison.OrdinalIgnoreCase))
            {
                error = "License is bound to another device.";
                return false;
            }

            payload = p;
            return true;
        }
        catch (Exception ex)
        {
            error = $"License verification failed: {ex.Message}";
            return false;
        }
    }

    public string PublicKeyPath => _publicKeyPath;

    public sealed record OfflineLicensePayload(string Key, string Plan, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, string? HardwareId);
    private sealed record SignedLicense(OfflineLicensePayload Payload, string SignatureBase64);
}
