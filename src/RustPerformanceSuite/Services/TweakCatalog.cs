namespace RustPerformanceSuite.Services;

public sealed record TweakDefinition(string Id, string Name, string Category, string Description, bool Reversible, bool Implemented);

public static class TweakCatalog
{
    private static readonly string[] Names =
    [
        "Game Mode", "Game DVR", "Background capture", "App capture", "Transparency", "Visual animations", "Window animations", "Menu animations", "Taskbar animations", "Minimize animations",
        "Mouse acceleration", "Mouse trails", "Keyboard repeat", "Power plan", "USB selective suspend", "PCIe link state", "Display timeout", "Sleep timeout", "Screen saver", "Fast startup",
        "Startup apps audit", "Background apps audit", "Windows Search audit", "Delivery Optimization audit", "OneDrive startup audit", "Widgets startup audit", "Teams startup audit", "Game Bar startup audit", "Cloud clipboard audit", "Location startup audit",
        "Notifications audit", "Focus Assist", "Xbox services audit", "Print spooler audit", "Bluetooth audit", "Fax service audit", "Remote Registry audit", "Maps service audit", "Retail Demo audit", "Smart Card audit",
        "Power throttling audit", "Multimedia scheduler audit", "Network throttling audit", "TCP autotuning audit", "DNS cache audit", "IPv6 audit", "Nagle audit", "ECN audit", "RSS audit", "Receive buffers audit",
        "GPU scheduling check", "Hardware acceleration check", "Fullscreen optimization check", "Variable refresh check", "HDR check", "Refresh rate check", "Resolution check", "Scaling check", "Color depth check", "Multi-monitor check",
        "Rust process priority", "Rust affinity recommendation", "Rust launch profile", "Rust shader cache audit", "Rust config backup", "Rust config validation", "Rust graphics profile", "Rust texture profile", "Rust shadow profile", "Rust effects profile",
        "Rust tree quality profile", "Rust water quality profile", "Rust particle profile", "Rust draw distance profile", "Rust UI profile", "Rust input profile", "Rust audio profile", "Rust network profile", "Rust benchmark profile", "Rust rollback profile"
    ];

    public static IReadOnlyList<TweakDefinition> All { get; } = Names.Select((name, i) => new TweakDefinition($"TWEAK-{i + 1:000}", name, i < 20 ? "Windows" : i < 40 ? "System" : i < 60 ? "Display" : "Rust", "Review and apply only when supported by the current Windows/Rust configuration.", true, i < 4)).ToArray();
}
