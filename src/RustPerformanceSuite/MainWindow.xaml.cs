using System.Windows;
using System.Windows.Threading;
using RustPerformanceSuite.Services;

namespace RustPerformanceSuite;

public partial class MainWindow : Window
{
    readonly UndOptiRuntime _app = UndOptiRuntime.Instance;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly SystemMonitor _monitor = new();
    readonly HardwareAnalyzer _hardware = new();
    readonly RustProfileService _profiles = new();
    readonly OptimizationSuite _suite = new();

    public MainWindow()
    {
        InitializeComponent();
        _monitor.CpuUpdated += v => Dispatcher.Invoke(() => CpuText.Text = $"CPU {v:0}%");
        _monitor.RamUpdated += v => Dispatcher.Invoke(() => RamText.Text = $"RAM {v:0} MB free");
        _timer.Tick += (_, _) => RefreshMetrics();
        _timer.Start();
        RefreshMetrics();
        ShowHardwareReport();
        RecommendationsText.Text = _suite.PerformanceRecommendations();
        DiagnosticsText.Text = _suite.NetworkReport();
    }

    void RefreshMetrics()
    {
        RustText.Text = _app.RustRunning() ? "RUST RUNNING" : "RUST READY";
        ChangesText.Text = $"CHANGES {_app.Changes.Count}";
        try
        {
            var report = _hardware.Analyze();
            GpuText.Text = report.Gpu.Length > 28 ? report.Gpu[..28] + "…" : report.Gpu;
        }
        catch { GpuText.Text = "GPU —"; }
    }

    void SetStatus(string text, bool good = true)
    {
        StatusText.Text = good ? "READY" : "CHECK";
        StatusText.Foreground = good ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.Orange;
        OperationStatus.Text = text;
        ChangesText.Text = $"CHANGES {_app.Changes.Count}";
    }

    void ShowHardwareReport()
    {
        HardwareReport.Text = _app.HardwareSummary() + "\n\n" + _suite.AdvancedHardwareReport();
    }

    void OptimizeAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var windows = _suite.OptimizeWindows();
            var power = _suite.SelectHighPerformancePowerPlan();
            var dns = _suite.FlushDns();
            var rust = _suite.SetRustPriority();
            SetStatus($"Full optimization finished. Windows={windows.Count(x => x.Changed)}, power={power.Changed}, DNS={dns.Changed}, Rust priority={rust.Changed}.");
            DiagnosticsText.Text = _suite.NetworkReport();
        }
        catch (Exception ex) { SetStatus("Optimization error: " + ex.Message, false); }
    }

    void Windows_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var r = _suite.OptimizeWindows();
            SetStatus($"Windows gaming profile: {r.Count(x => x.Changed)} new tracked change(s).");
        }
        catch (Exception ex) { SetStatus("Windows optimization failed: " + ex.Message, false); }
    }

    void Cleaner_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var r = _suite.CleanTempFiles();
            SetStatus($"Cleaner finished: {r.Count(x => x.Changed)} cleanup action(s) changed something.");
        }
        catch (Exception ex) { SetStatus("Cleaner failed: " + ex.Message, false); }
    }

    void Network_Click(object sender, RoutedEventArgs e)
    {
        var r = _suite.FlushDns();
        DiagnosticsText.Text = _suite.NetworkReport();
        SetStatus(r.Details.Length > 0 ? $"Network: {r.Details}" : "DNS cache flushed.", r.Changed);
    }

    void NetworkTest_Click(object sender, RoutedEventArgs e)
    {
        DiagnosticsText.Text = _suite.NetworkReport();
        SetStatus("Network diagnostics refreshed.");
    }

    void Power_Click(object sender, RoutedEventArgs e)
    {
        var r = _suite.SelectHighPerformancePowerPlan();
        SetStatus(r.Details, r.Changed);
    }

    void RestorePower_Click(object sender, RoutedEventArgs e)
    {
        var r = _suite.RestorePowerPlan();
        SetStatus(r.Details, r.Changed);
    }

    void Hardware_Click(object sender, RoutedEventArgs e)
    {
        ShowHardwareReport();
        RecommendationsText.Text = _suite.PerformanceRecommendations();
        SetStatus("Hardware and BIOS advisor refreshed. No firmware or blind voltage changes were made.");
    }

    void Rust_Click(object sender, RoutedEventArgs e)
    {
        var profile = _profiles.Describe(RustProfile.Competitive);
        var results = _suite.OptimizeRust();
        SetStatus("Rust Competitive profile: " + profile + $" Priority action changed={results.Last().Changed}.");
    }

    void RustPriority_Click(object sender, RoutedEventArgs e)
    {
        var r = _suite.SetRustPriority();
        SetStatus(r.Details, r.Changed);
    }

    void Shader_Click(object sender, RoutedEventArgs e)
    {
        var r = _suite.CleanShaderCache();
        SetStatus(r.Details, r.Changed);
    }

    void Startup_Click(object sender, RoutedEventArgs e)
    {
        DiagnosticsText.Text = "STARTUP REPORT\n\n" + _suite.StartupReport();
        SetStatus("Startup report generated. No startup applications were disabled automatically.");
    }

    void Restore_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var registry = _app.Restore();
            var power = _suite.RestorePowerPlan();
            SetStatus($"Restore finished: {registry} tracked registry change(s); power plan restored={power.Changed}.");
        }
        catch (Exception ex) { SetStatus("Restore failed: " + ex.Message, false); }
    }

    void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshMetrics();
        ShowHardwareReport();
        RecommendationsText.Text = _suite.PerformanceRecommendations();
        DiagnosticsText.Text = _suite.NetworkReport();
        SetStatus("Dashboard refreshed.");
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _monitor.Dispose();
        base.OnClosed(e);
    }
}
