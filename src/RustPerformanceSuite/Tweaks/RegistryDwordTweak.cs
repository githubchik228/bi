using Microsoft.Win32;
using RustPerformanceSuite.Models;

namespace RustPerformanceSuite.Tweaks;

public sealed class RegistryDwordTweak : ITweak
{
    private readonly string _path;
    private readonly string _name;
    private readonly int _value;
    private readonly RegistryHive _hive;
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }

    public RegistryDwordTweak(string id, string name, string description, RegistryHive hive, string path, string nameValue, int value)
    { Id = id; Name = name; Description = description; _hive = hive; _path = path; _name = nameValue; _value = value; }

    public Task<AppliedChange?> ApplyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var key = RegistryKey.OpenBaseKey(_hive, RegistryView.Default).CreateSubKey(_path, true);
            var old = key?.GetValue(_name, null);
            key?.SetValue(_name, _value, RegistryValueKind.DWord);
            return Task.FromResult<AppliedChange?>(new AppliedChange { TweakId = Id, Description = Name, OriginalValue = old?.ToString() ?? "<missing>", AppliedValue = _value.ToString() });
        }
        catch { return Task.FromResult<AppliedChange?>(null); }
    }

    public Task<bool> RestoreAsync(AppliedChange change, CancellationToken cancellationToken = default)
    {
        try
        {
            using var key = RegistryKey.OpenBaseKey(_hive, RegistryView.Default).CreateSubKey(_path, true);
            var current = key?.GetValue(_name, null)?.ToString();
            if (current != change.AppliedValue) return Task.FromResult(false);
            if (change.OriginalValue == "<missing>") key?.DeleteValue(_name, false);
            else if (int.TryParse(change.OriginalValue, out var old)) key?.SetValue(_name, old, RegistryValueKind.DWord);
            else return Task.FromResult(false);
            return Task.FromResult(true);
        }
        catch { return Task.FromResult(false); }
    }
}