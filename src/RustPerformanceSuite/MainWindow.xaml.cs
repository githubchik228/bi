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
        StatusText.Text = text;
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
            SetStatus($"Optimization complete: {windows.Count(x => x.Changed)} Windows changes, power plan={power.Changed}, DNS={dns.Changed}.");
        }
        catch (Exception ex) { SetStatus("Optimization error: " + ex.Message, false); }
    }

    void Windows_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var r = _suite.OptimizeWindows();
            SetStatus($"Windows optimization: {r.Count(x => x.Changed)} changes applied.");
        }
        catch (Exception ex) { SetStatus("Windows optimization failed: " + ex.Message, false); }
    }

    void Cleaner_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var r = _suite.CleanTempFiles();
            SetStatus($"Cleaner finished: {r.Sum(x => x.Changed ? 1 : 0)} locations processed.");
        }
        catch (Exception ex) { SetStatus("Cleaner failed: " + ex.Message, false); }
    }

    void Network_Click(object sender, RoutedEventArgs e)
    {
        var r = _suite.FlushDns();
        SetStatus(r.Details.Length > 0 ? $"Network: {r.Details}" : "DNS cache flushed.", r.Changed);
    }

    void Power_Click(object sender, RoutedEventArgs e)
    {
        var r = _suite.SelectHighPerformancePowerPlan();
        SetStatus(r.Changed ? "High-performance power plan selected." : "Power plan was not changed.", r.Changed);
    }

    void Hardware_Click(object sender, RoutedEventArgs e)
    {
        ShowHardwareReport();
        SetStatus("Hardware analysis refreshed. OC/undervolt recommendations are hardware-specific.");
    }

    void Rust_Click(object sender, RoutedEventArgs e)
    {
        var profile = _profiles.Describe(RustProfile.Competitive);
        SetStatus("Rust Competitive profile selected: " + profile);
    }

    void Restore_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var n = _app.Restore();
            SetStatus($"Restored {n} tracked changes.");
        }
        catch (Exception ex) { SetStatus("Restore failed: " + ex.Message, false); }
    }

    void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshMetrics();
        ShowHardwareReport();
        SetStatus("Dashboard refreshed.");
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _monitor.Dispose();
        base.OnClosed(e);
    }
}
