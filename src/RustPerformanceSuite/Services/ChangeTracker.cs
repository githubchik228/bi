using System.Text.Json;
using RustPerformanceSuite.Models;

namespace RustPerformanceSuite.Services;

public sealed class ChangeTracker
{
    private readonly string _file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RustPerformanceSuite", "changes.json");
    private readonly object _sync = new();
    public List<AppliedChange> Changes { get; private set; } = new();

    public ChangeTracker() => Load();

    public void Add(AppliedChange change) { lock (_sync) { Changes.Add(change); Save(); } }
    public void MarkRestored(string id) { lock (_sync) { var c = Changes.FirstOrDefault(x => x.Id == id); if (c != null) c.Restored = true; Save(); } }

    private void Load()
    {
        try { if (File.Exists(_file)) Changes = JsonSerializer.Deserialize<List<AppliedChange>>(File.ReadAllText(_file)) ?? new(); }
        catch { Changes = new(); }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, JsonSerializer.Serialize(Changes, new JsonSerializerOptions { WriteIndented = true }));
    }
}