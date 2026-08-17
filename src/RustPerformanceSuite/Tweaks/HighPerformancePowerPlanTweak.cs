using RustPerformanceSuite.Models;
using RustPerformanceSuite.Services;

namespace RustPerformanceSuite.Tweaks;

public sealed class HighPerformancePowerPlanTweak : ITweak
{
    private readonly PowerPlanService _power = new();
    public string Id => "power.high-performance";
    public string Name => "High performance power plan";
    public string Description => "Temporarily switches Windows to the existing High performance plan.";

    public Task<AppliedChange?> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var original = _power.GetActiveScheme();
        if (string.IsNullOrWhiteSpace(original) || !_power.SetHighPerformance()) return Task.FromResult<AppliedChange?>(null);
        return Task.FromResult<AppliedChange?>(new AppliedChange { TweakId = Id, Description = Name, OriginalValue = original, AppliedValue = _power.GetActiveScheme() });
    }

    public Task<bool> RestoreAsync(AppliedChange change, CancellationToken cancellationToken = default)
        => Task.FromResult(_power.GetActiveScheme().Equals(change.AppliedValue, StringComparison.OrdinalIgnoreCase) && _power.Restore(change.OriginalValue));
}