namespace RustPerformanceSuite.Services;

public enum RustProfile { Competitive, Balanced, Quality, LowEnd }

public sealed class RustProfileService
{
    public string Describe(RustProfile profile) => profile switch
    {
        RustProfile.Competitive => "Prioritizes latency and stable frametimes with conservative visual changes.",
        RustProfile.Balanced => "Balanced CPU/GPU load with conservative Windows game settings.",
        RustProfile.Quality => "Keeps image quality high and avoids performance trade-offs.",
        RustProfile.LowEnd => "Prioritizes consistency on lower-end hardware.",
        _ => "Unknown profile."
    };

    public string[] RecommendedLaunchArguments(RustProfile profile) => profile switch
    {
        RustProfile.Competitive => ["-high"],
        RustProfile.Balanced => [],
        RustProfile.Quality => [],
        RustProfile.LowEnd => [],
        _ => []
    };
}
