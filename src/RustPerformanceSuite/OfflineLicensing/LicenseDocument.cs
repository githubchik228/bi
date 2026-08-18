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

public sealed record SignedLicense(
    LicensePayload Payload,
    string SignatureBase64)
{
    public static SignedLicense Parse(string json)
    {
        return JsonSerializer.Deserialize<SignedLicense>(json)
            ?? throw new InvalidDataException(
                "Invalid license file.");
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(
            this,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });
    }
}

public static class OfflineLicenseVerifier
{
    // IMPORTANT:
    // Paste ONLY the RSA PUBLIC key here.
    // NEVER paste the private key.
    private const string PublicKeyPem = "";

    public static bool Verify(
        SignedLicense license,
        string hardwareId,
        out string error)
    {
        error = "";

        if (string.IsNullOrWhiteSpace(PublicKeyPem))
        {
            error = "license_public_key_not_configured";
            return false;
        }

        if (license.Payload.ExpiresAt is not null &&
            license.Payload.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            error = "expired";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(
                license.Payload.HardwareId) &&
            !string.Equals(
                license.Payload.HardwareId,
                hardwareId,
                StringComparison.OrdinalIgnoreCase))
        {
            error = "hardware_mismatch";
            return false;
        }

        if (string.IsNullOrWhiteSpace(
                license.Payload.Key))
        {
            error = "missing_key";
            return false;
        }

        if (string.IsNullOrWhiteSpace(
                license.Payload.Plan))
        {
            error = "missing_plan";
            return false;
        }

        try
        {
            var data =
                CanonicalPayload(
                    license.Payload);

            using var rsa = RSA.Create();

            rsa.ImportFromPem(
                PublicKeyPem);

            byte[] signature;

            try
            {
                signature =
                    Convert.FromBase64String(
                        license.SignatureBase64);
            }
            catch (FormatException)
            {
                error =
                    "invalid_signature_encoding";

                return false;
            }

            var valid =
                rsa.VerifyData(
                    Encoding.UTF8.GetBytes(data),
                    signature,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pss);

            if (!valid)
            {
                error =
                    "invalid_signature";

                return false;
            }

            return true;
        }
        catch
        {
            error =
                "invalid_license";

            return false;
        }
    }

    public static string CanonicalPayload(
        LicensePayload payload)
    {
        return string.Join(
            "|",
            payload.Key
                .Trim()
                .ToUpperInvariant(),

            payload.Plan
                .Trim()
                .ToLowerInvariant(),

            payload.CreatedAt
                .ToUniversalTime()
                .ToString("O"),

            payload.ExpiresAt?
                .ToUniversalTime()
                .ToString("O")
                ?? "",

            payload.HardwareId?
                .Trim()
                ?? "");
    }
}
