using System.Text.Json;

namespace RustPerformanceSuite.Services;

public sealed class BackupService
{
    private readonly string _root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UndOpti", "Backups");

    public string Create(string sourceFile)
    {
        Directory.CreateDirectory(_root);
        var target = Path.Combine(_root, $"backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
        var payload = new { CreatedUtc = DateTime.UtcNow, Source = sourceFile, Content = File.Exists(sourceFile) ? File.ReadAllText(sourceFile) : "" };
        File.WriteAllText(target, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        return target;
    }

    public IReadOnlyList<string> List() => Directory.Exists(_root) ? Directory.GetFiles(_root, "*.json").OrderDescending().ToArray() : [];
}
