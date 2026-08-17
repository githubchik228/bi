using System.Windows;
using RustPerformanceSuite.Services;

namespace RustPerformanceSuite;

public partial class App : Application
{
    private readonly LicenseService _license = new();
    private readonly OptimizationEngine _optimizer;

    public App()
    {
        _optimizer = new OptimizationEngine(new ChangeTracker());
        Startup += OnStartup;
    }

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        if (_license.HasExpired)
            await _optimizer.RestoreAllAsync();
    }
}
