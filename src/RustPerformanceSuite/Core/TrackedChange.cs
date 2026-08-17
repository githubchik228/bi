namespace RustPerformanceSuite.Core;

public sealed record TrackedChange(
    string Id,
    string Category,
    string Target,
    string OriginalValue,
    string AppliedValue,
    DateTime AppliedAtUtc);
