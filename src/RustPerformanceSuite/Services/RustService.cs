namespace RustPerformanceSuite.Services;

public sealed class RustService
{
    public string? FindInstall()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Rust"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Rust")
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    public bool IsRustRunning() => System.Diagnostics.Process.GetProcessesByName("RustClient").Length > 0;
}