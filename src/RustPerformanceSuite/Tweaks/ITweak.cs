using RustPerformanceSuite.Models;

namespace RustPerformanceSuite.Tweaks;

public interface ITweak
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    Task<AppliedChange?> ApplyAsync(CancellationToken cancellationToken = default);
    Task<bool> RestoreAsync(AppliedChange change, CancellationToken cancellationToken = default);
}