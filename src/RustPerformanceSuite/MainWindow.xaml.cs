using System.Windows;
using System.Windows.Threading;
using RustPerformanceSuite.Services;

namespace RustPerformanceSuite;

public partial class MainWindow : Window
{
    private readonly LicenseService _license = new();
    private readonly ChangeTracker _tracker = new();
    private readonly OptimizationEngine _optimizer;
    private readonly SystemMonitorService _monitor = new();
    private readonly RustService _rust = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public MainWindow()
    {
        InitializeComponent();
        _optimizer = new OptimizationEngine(_tracker);
        HardwareIdText.Text = $"HWID: {_license.HardwareId[..12]}…";
        TweaksText.Text = $"{_optimizer.Tweaks.Count}";
        RefreshStatus();
        _timer.Tick += (_, _) => RefreshMetrics();
        _timer.Start();
    }

    private void RefreshMetrics()
    {
        var sample = _monitor.Sample();
        CpuText.Text = $"{sample.CpuPercent:0}%";
        RamText.Text = $"{sample.MemoryPercent:0}%";
        RustText.Text = _rust.IsRustRunning() ? "Running" : (_rust.FindInstall() is not null ? "Installed" : "Not detected");
    }

    private void RefreshStatus()
    {
        if (_license.IsLicensed)
        {
            LicenseStatus.Text = "● LICENSE ACTIVE";
            LicenseStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
        else
        {
            LicenseStatus.Text = _license.IsExpired ? "● LICENSE EXPIRED" : "● LICENSE REQUIRED";
            LicenseStatus.Foreground = System.Windows.Media.Brushes.Orange;
        }
    }

    private async void Optimize_Click(object sender, RoutedEventArgs e)
    {
        if (!_license.IsLicensed) { OperationStatus.Text = "A valid license is required."; return; }
        try
        {
            OperationStatus.Text = "Applying reversible optimizations…";
            var changes = await _optimizer.ApplyAllAsync();
            OperationStatus.Text = $"Applied {changes.Count} changes. Backup records created.";
        }
        catch (Exception ex) { OperationStatus.Text = $"Optimization stopped: {ex.Message}"; }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var restored = await _optimizer.RestoreAllAsync();
            OperationStatus.Text = $"Restored {restored} tracked changes.";
        }
        catch (Exception ex) { OperationStatus.Text = $"Restore stopped: {ex.Message}"; }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) { _license.Load(); RefreshStatus(); RefreshMetrics(); }

    private async void Activate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            OperationStatus.Text = "Contacting license server…";
            var ok = await _license.ActivateRemoteAsync(LicenseEndpoint.Text, LicenseKey.Text.Trim());
            OperationStatus.Text = ok ? "License activated successfully." : "License activation failed.";
            RefreshStatus();
        }
        catch (Exception ex) { OperationStatus.Text = $"License server error: {ex.Message}"; }
    }
}