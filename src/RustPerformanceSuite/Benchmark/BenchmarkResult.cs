namespace RustPerformanceSuite.Benchmark;

public sealed record BenchmarkResult(
    DateTime StartedAtUtc,
    TimeSpan Duration,
    double AverageFps,
    double OnePercentLow,
    double AverageFrameTimeMs,
    string Source);
