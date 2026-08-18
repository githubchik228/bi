using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace UndOptiKeygen;

public partial class MainWindow : Window
{
    private readonly RSA _rsa;
    private readonly string _keyDirectory;
    private readonly string _privateKeyPath;
    private readonly string _publicKeyPath;

    public MainWindow()
    {
        InitializeComponent();

        _keyDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "UndOpti",
            "Licensing");

        Directory.CreateDirectory(_keyDirectory);

        _privateKeyPath = Path.Combine(
            _keyDirectory,
            "undopti-private.pem");

        _publicKeyPath = Path.Combine(
            _keyDirectory,
            "undopti-public.pem");

        _rsa = RSA.Create(3072);

        LoadOrCreateKeys();

        HwidBox.Text = GetHardwareId();
    }

    private void LoadOrCreateKeys()
    {
        if (File.Exists(_privateKeyPath))
        {
            _rsa.ImportFromPem(
                File.ReadAllText(_privateKeyPath));
        }
        else
        {
            File.WriteAllText(
                _privateKeyPath,
                _rsa.ExportRSAPrivateKeyPem(),
                new UTF8Encoding(false));
        }

        if (!File.Exists(_publicKeyPath))
        {
            File.WriteAllText(
                _publicKeyPath,
                _rsa.ExportRSAPublicKeyPem(),
                new UTF8Encoding(false));
        }

        try
        {
            File.SetAttributes(
                _privateKeyPath,
                FileAttributes.Hidden);
        }
        catch
        {
            // Optional.
        }

        StatusBox.Text =
            "Signing keys ready.\n\n" +
            $"Private key:\n{_privateKeyPath}\n\n" +
            $"Public key:\n{_publicKeyPath}";
    }

    private void GenerateButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            var plan = GetSelectedPlan();

            var createdAt =
                DateTimeOffset.UtcNow;

            DateTimeOffset? expiresAt =
                plan switch
                {
                    "Day" =>
                        createdAt.AddDays(1),

                    "SevenDays" =>
                        createdAt.AddDays(7),

                    "Month" =>
                        createdAt.AddDays(30),

                    "Year" =>
                        createdAt.AddYears(1),

                    "Lifetime" =>
                        null,

                    _ =>
                        createdAt.AddDays(30)
                };

            var key =
                GenerateLicenseKey();

            var hardwareId =
                string.IsNullOrWhiteSpace(HwidBox.Text)
                    ? null
                    : HwidBox.Text.Trim();

            var payload =
                new LicensePayload(
                    key,
                    plan,
                    createdAt,
                    expiresAt,
                    hardwareId);

            var canonical =
                CanonicalPayload(payload);

            var signature =
                _rsa.SignData(
                    Encoding.UTF8.GetBytes(canonical),
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pss);

            var license =
                new SignedLicense(
                    payload,
                    Convert.ToBase64String(signature));

            var outputDirectory =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "licenses");

            Directory.CreateDirectory(
                outputDirectory);

            var outputPath =
                Path.Combine(
                    outputDirectory,
                    $"{key}.license.json");

            var json =
                JsonSerializer.Serialize(
                    license,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

            File.WriteAllText(
                outputPath,
                json,
                new UTF8Encoding(false));

            KeyBox.Text = key;

            StatusBox.Text =
                "LICENSE CREATED\n\n" +
                $"Key: {key}\n" +
                $"Plan: {plan}\n" +
                $"HWID: {hardwareId ?? "Not bound"}\n" +
                $"Expires: " +
                (expiresAt.HasValue
                    ? expiresAt.Value
                        .ToLocalTime()
                        .ToString("yyyy-MM-dd HH:mm:ss")
                    : "Never") +
                "\n\n" +
                $"License:\n{outputPath}\n\n" +
                $"Public key:\n{_publicKeyPath}";
        }
        catch (Exception ex)
        {
            StatusBox.Text =
                "LICENSE GENERATION FAILED\n\n" +
                ex.Message;
        }
    }

    private string GetSelectedPlan()
    {
        if (PlanBox.SelectedItem is
            System.Windows.Controls.ComboBoxItem item &&
            item.Content != null)
        {
            var value =
                item.Content
                    .ToString()!
                    .Trim();

            return value switch
            {
                "1 day" => "Day",
                "7 days" => "SevenDays",
                "30 days" => "Month",
                "1 year" => "Year",
                "Lifetime" => "Lifetime",

                "Day" => "Day",
                "SevenDays" => "SevenDays",
                "Month" => "Month",
                "Year" => "Year",
                "Lifetime" => "Lifetime",

                _ => "Month"
            };
        }

        return "Month";
    }

    private static string GenerateLicenseKey()
    {
        return
            "UND-" +
            RandomNumberGenerator.GetHexString(4) +
            "-" +
            RandomNumberGenerator.GetHexString(4) +
            "-" +
            RandomNumberGenerator.GetHexString(4) +
            "-" +
            RandomNumberGenerator.GetHexString(4);
    }

    private static string GetHardwareId()
    {
        using var sha =
            SHA256.Create();

        var raw =
            Environment.MachineName;

        var hash =
            sha.ComputeHash(
                Encoding.UTF8.GetBytes(raw));

        return Convert.ToHexString(hash);
    }

    private static string CanonicalPayload(
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

    private sealed record LicensePayload(
        string Key,
        string Plan,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ExpiresAt,
        string? HardwareId);

    private sealed record SignedLicense(
        LicensePayload Payload,
        string SignatureBase64);
}
