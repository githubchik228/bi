namespace RustPerformanceSuite.Services;

public sealed record BenchmarkSnapshot(double AverageFps, double OnePercentLow, double AverageFrameTimeMs, int Samples);

public sealed class BenchmarkService
{
    public BenchmarkSnapshot Measure(IEnumerable<double> frameTimesMs)
    {
        var samples = frameTimesMs.Where(x => x > 0 && double.IsFinite(x)).OrderBy(x => x).ToArray();
        if (samples.Length == 0) return new(0, 0, 0, 0);
        var avgMs = samples.Average();
        var tail = Math.Max(1, (int)Math.Ceiling(samples.Length * 0.01));
        var lowMs = samples[^tail..].Average();
        return new(1000.0 / avgMs, 1000.0 / lowMs, avgMs, samples.Length);
    }

    public static double PercentChange(double before, double after) => before == 0 ? 0 : (after - before) / before * 100.0;
}
