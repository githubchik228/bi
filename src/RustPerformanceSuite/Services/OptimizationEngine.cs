using Microsoft.Win32;
using RustPerformanceSuite.Models;
using RustPerformanceSuite.Tweaks;

namespace RustPerformanceSuite.Services;

public sealed class OptimizationEngine
{
    private readonly ChangeTracker _tracker;
    public IReadOnlyList<ITweak> Tweaks { get; }

    public OptimizationEngine(ChangeTracker tracker)
    {
        _tracker = tracker;
        Tweaks = new ITweak[]
        {
            new HighPerformancePowerPlanTweak(),
            new RegistryDwordTweak("windows.game-mode", "Windows Game Mode", "Enables Game Mode for the current machine.", RegistryHive.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1),
            new RegistryDwordTweak("windows.gamedvr", "Game DVR background capture", "Disables background Game DVR capture; reversible.", RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 0),
            new RegistryDwordTweak("windows.transparency", "Transparency effects", "Disables transparency effects for a simpler desktop compositor workload.", RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0)
        };
    }

    public async Task<IReadOnlyList<AppliedChange>> ApplyAllAsync(CancellationToken token = default)
    {
        var applied = new List<AppliedChange>();
        foreach (var tweak in Tweaks)
        {
            token.ThrowIfCancellationRequested();
            var change = await tweak.ApplyAsync(token);
            if (change is not null) { _tracker.Add(change); applied.Add(change); }
        }
        return applied;
    }

    public async Task<int> RestoreAllAsync(CancellationToken token = default)
    {
        var restored = 0;
        foreach (var change in _tracker.Changes.Where(c => !c.Restored).Reverse())
        {
            token.ThrowIfCancellationRequested();
            var tweak = Tweaks.FirstOrDefault(t => t.Id == change.TweakId);
            if (tweak is not null && await tweak.RestoreAsync(change, token)) { _tracker.MarkRestored(change.Id); restored++; }
        }
        return restored;
    }
}