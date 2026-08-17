using System.Text.Json;

namespace RustPerformanceSuite.Core;

public sealed class ChangeTracker
{
    private readonly string _path;
    private readonly List<TrackedChange> _changes = [];
    private readonly object _gate = new();

    public ChangeTracker()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UndOpti");
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "changes.json");
        Load();
    }

    public IReadOnlyList<TrackedChange> Changes { get { lock (_gate) return _changes.ToArray(); } }

    public void Track(TrackedChange change)
    {
        lock (_gate)
        {
            _changes.RemoveAll(x => x.Id == change.Id);
            _changes.Add(change);
            Save();
        }
    }

    public void Remove(string id)
    {
        lock (_gate) { _changes.RemoveAll(x => x.Id == id); Save(); }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var data = JsonSerializer.Deserialize<List<TrackedChange>>(File.ReadAllText(_path));
            if (data is not null) _changes.AddRange(data);
        }
        catch { _changes.Clear(); }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_changes, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, json);
    }
}
