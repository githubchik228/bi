using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
namespace UndOptiKeygen;
public partial class MainWindow : Window
{
    private const string KeyDirName = "UndOpti\\Licensing";
    private RSA _rsa = RSA.Create(3072);
    private readonly string _keyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), KeyDirName, "undopti-signing-private.pem");
    public MainWindow() { InitializeComponent(); LoadKey(); RoleBox.SelectionChanged += (_, _) => { if (((System.Windows.Controls.ComboBoxItem)RoleBox.SelectedItem).Content?.ToString() == "OWNER") PlanBox.SelectedIndex = 4; }; }
    private void LoadKey()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_keyPath)!);
        if (File.Exists(_keyPath)) _rsa.ImportFromPem(File.ReadAllText(_keyPath));
        else { File.WriteAllText(_keyPath, _rsa.ExportRSAPrivateKeyPem(), new UTF8Encoding(false)); File.SetAttributes(_keyPath, FileAttributes.Hidden); MessageBox.Show("A new signing key was created locally. Keep the PEM file private.", "UndOpti", MessageBoxButton.OK, MessageBoxImage.Information); }
    }
    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        var role = ((System.Windows.Controls.ComboBoxItem)RoleBox.SelectedItem).Content?.ToString() ?? "USER";
        var plan = ((System.Windows.Controls.ComboBoxItem)PlanBox.SelectedItem).Content?.ToString()?.ToLowerInvariant() ?? "lifetime";
        if (role == "OWNER") plan = "lifetime";
        var created = DateTimeOffset.UtcNow;
        DateTimeOffset? expires = plan switch { "1d" => created.AddDays(1), "7d" => created.AddDays(7), "30d" => created.AddDays(30), "1y" => created.AddYears(1), _ => null };
        var key = $"UND-{RandomNumberGenerator.GetHexString(4)}-{RandomNumberGenerator.GetHexString(4)}-{RandomNumberGenerator.GetHexString(4)}-{RandomNumberGenerator.GetHexString(4)}";
        var hwid = string.IsNullOrWhiteSpace(HwidBox.Text) ? null : HwidBox.Text.Trim();
        var canonical = string.Join("|", key, plan, role, created.ToUniversalTime().ToString("O"), expires?.ToUniversalTime().ToString("O") ?? "", hwid ?? "");
        var sig = Convert.ToBase64String(_rsa.SignData(Encoding.UTF8.GetBytes(canonical), HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
        var license = new { Payload = new { Key = key, Plan = plan, Role = role, CreatedAt = created, ExpiresAt = expires, HardwareId = hwid }, SignatureBase64 = sig };
        var dir = Path.Combine(AppContext.BaseDirectory, "licenses"); Directory.CreateDirectory(dir); var file = Path.Combine(dir, key + ".license.json"); File.WriteAllText(file, JsonSerializer.Serialize(license, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        OutputBox.Text = $"Key: {key}\nRole: {role}\nPlan: {plan}\nExpires: {(expires?.ToLocalTime().ToString() ?? "Never")}\nSaved: {file}";
    }
}
