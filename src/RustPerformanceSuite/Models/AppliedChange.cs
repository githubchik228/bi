namespace RustPerformanceSuite.Models;

public sealed class AppliedChange
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string TweakId { get; init; } = "";
    public string Description { get; init; } = "";
    public string OriginalValue { get; init; } = "";
    public string AppliedValue { get; init; } = "";
    public DateTimeOffset AppliedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool Restored { get; set; }
}