namespace RustPerformanceSuite.Benchmark;

public sealed class BenchmarkService
{
    private readonly List<double> _frameTimesMs = [];

    public void RecordFrame(double frameTimeMs)
    {
        if (frameTimeMs > 0 && double.IsFinite(frameTimeMs)) _frameTimesMs.Add(frameTimeMs);
    }

    public BenchmarkResult Finish(TimeSpan duration, string source = "UndOpti")
    {
        if (_frameTimesMs.Count == 0)
            return new BenchmarkResult(DateTime.UtcNow, duration, 0, 0, 0, source);

        var ordered = _frameTimesMs.OrderBy(x => x).ToArray();
        var avgFrame = _frameTimesMs.Average();
        var onePercentCount = Math.Max(1, (int)Math.Ceiling(ordered.Length * 0.01));
        var slowest = ordered.TakeLast(onePercentCount).Average();
        var avgFps = 1000.0 / avgFrame;
        var onePercentLow = 1000.0 / slowest;
        return new BenchmarkResult(DateTime.UtcNow, duration, avgFps, onePercentLow, avgFrame, source);
    }

    public void Reset() => _frameTimesMs.Clear();
}
