using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
namespace UndOptiKeygen;
public partial class MainWindow : Window
{
    private readonly string _keyDirectory;
    private readonly string _privateKeyPath;
    private RSA _rsa;
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
            "undopti-signing-private.pem");
        _rsa = RSA.Create(3072);
        LoadOrCreatePrivateKey();
        HwidBox.Text =
            Services.HwidService.GetHardwareId();
    }
    private void LoadOrCreatePrivateKey()
    {
        if (File.Exists(_privateKeyPath))
        {
            _rsa.ImportFromPem(
                File.ReadAllText(_privateKeyPath));
            return;
        }
        var privateKey =
            _rsa.ExportRSAPrivateKeyPem();
        File.WriteAllText(
            _privateKeyPath,
            privateKey,
            new UTF8Encoding(false));
        File.SetAttributes(
            _privateKeyPath,
            FileAttributes.Hidden);
        StatusBox.Text =
            "A new private signing key was created.\n\n" +
            "KEEP IT PRIVATE.\n\n" +
            _privateKeyPath;
    }
    private void GenerateButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            var role =
                ((ComboBoxItem)RoleBox.SelectedItem)
                .Content?
                .ToString()?
                .ToUpperInvariant() ?? "USER";
            var selectedPlan =
                ((ComboBoxItem)PlanBox.SelectedItem)
                .Content?
                .ToString() ?? "30 days";
            var plan = ConvertPlan(selectedPlan);
            if (role == "OWNER")
                plan = "lifetime";
            var createdAt =
                DateTimeOffset.UtcNow;
            DateTimeOffset? expiresAt =
                plan switch
                {
                    "1d" =>
                        createdAt.AddDays(1),
                    "7d" =>
                        createdAt.AddDays(7),
                    "30d" =>
                        createdAt.AddDays(30),
                    "1y" =>
                        createdAt.AddYears(1),
                    _ =>
                        null
                };
            var key =
                $"UND-" +
                $"{RandomNumberGenerator.GetHexString(4)}-" +
                $"{RandomNumberGenerator.GetHexString(4)}-" +
                $"{RandomNumberGenerator.GetHexString(4)}-" +
                $"{RandomNumberGenerator.GetHexString(4)}";
            var hwid =
                string.IsNullOrWhiteSpace(HwidBox.Text)
                    ? null
                    : HwidBox.Text.Trim();
            var payload = new LicensePayload
            {
                Key = key,
                Role = role,
                Plan = plan,
                CreatedAt = createdAt,
                ExpiresAt = expiresAt,
                HardwareId = hwid
            };
            var canonical =
                BuildCanonical(payload);
            var signature =
                _rsa.SignData(
                    Encoding.UTF8.GetBytes(canonical),
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pss);
            var license =
                new SignedLicense
                {
                    Payload = payload,
                    SignatureBase64 =
                        Convert.ToBase64String(signature)
                };
            var outputDirectory =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "licenses");
            Directory.CreateDirectory(
                outputDirectory);
            var outputFile =
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
                outputFile,
                json,
                new UTF8Encoding(false));
            KeyBox.Text = key;
            StatusBox.Text =
                "LICENSE CREATED\n\n" +
                $"Role: {role}\n" +
                $"Plan: {plan}\n" +
                $"Expires: " +
                (expiresAt.HasValue
                    ? expiresAt.Value
                        .ToLocalTime()
                        .ToString()
                    : "Never") +
                "\n\n" +
                $"File:\n{outputFile}";
        }
        catch (Exception ex)
        {
            StatusBox.Text =
                "Generation failed:\n\n" +
                ex.Message;
        }
    }
    private static string ConvertPlan(
        string value)
    {
        return value switch
        {
            "1 day" => "1d",
            "7 days" => "7d",
            "30 days" => "30d",
            "1 year" => "1y",
            "Lifetime" => "lifetime",
            _ => "30d"
        };
    }
    private static string BuildCanonical(
        LicensePayload payload)
    {
        return string.Join(
            "|",
            payload.Key.ToUpperInvariant(),
            payload.Role.ToUpperInvariant(),
            payload.Plan.ToLowerInvariant(),
            payload.CreatedAt
                .ToUniversalTime()
                .ToString("O"),
            payload.ExpiresAt?
                .ToUniversalTime()
                .ToString("O") ?? "",
            payload.HardwareId ?? "");
    }
    private sealed class SignedLicense
    {
        public LicensePayload Payload { get; set; } = new();
        public string SignatureBase64 { get; set; } = "";
    }
    private sealed class LicensePayload
    {
        public string Key { get; set; } = "";
        public string Role { get; set; } = "";
        public string Plan { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public string? HardwareId { get; set; }
    }
}
