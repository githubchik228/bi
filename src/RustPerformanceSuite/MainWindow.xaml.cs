using System.Windows;
using System.Windows.Threading;

namespace RustPerformanceSuite;

public partial class MainWindow : Window
{
    readonly UndOptiRuntime _app = UndOptiRuntime.Instance;
    readonly DispatcherTimer _timer = new(){Interval=TimeSpan.FromSeconds(1)};
    DateTime _lastValidation=DateTime.MinValue;
    bool _rolledBack;
    public MainWindow(){
        InitializeComponent();
        HardwareIdText.Text=$"HWID: {_app.HardwareId[..12]}…";
        TweaksText.Text=$"{_app.Changes.Count}";
        RefreshStatus(); RefreshMetrics();
        _timer.Tick += async (_,_)=>await TickAsync(); _timer.Start();
    }
    async Task TickAsync(){
        RefreshMetrics();
        if(_app.IsLicensed && DateTime.UtcNow-_lastValidation>=TimeSpan.FromMinutes(1)){_lastValidation=DateTime.UtcNow; await _app.ValidateAsync(LicenseEndpoint.Text); RefreshStatus();}
        if(_app.License is not null && !_app.License.Active && !_rolledBack){_rolledBack=true; OperationStatus.Text="License expired — restoring UndOpti changes…"; var n=_app.Restore(); OperationStatus.Text=$"License expired. Restored {n} tracked changes."; RefreshStatus();}
    }
    void RefreshMetrics(){ CpuText.Text="Monitoring"; RamText.Text=$"{GC.GetGCMemoryInfo().TotalAvailableMemoryBytes/1073741824d:0.0} GB"; RustText.Text=_app.RustRunning()?"Running":"Not running"; }
    void RefreshStatus(){ if(_app.IsLicensed){LicenseStatus.Text="● LICENSE ACTIVE"; LicenseStatus.Foreground=System.Windows.Media.Brushes.LightGreen;}else{LicenseStatus.Text=_app.License is not null?"● LICENSE EXPIRED":"● LICENSE REQUIRED";LicenseStatus.Foreground=System.Windows.Media.Brushes.Orange;} }
    void Optimize_Click(object sender,RoutedEventArgs e){if(!_app.IsLicensed){OperationStatus.Text="A valid license is required.";return;} try{_app.ApplySafeProfile();TweaksText.Text=$"{_app.Changes.Count}";OperationStatus.Text=$"Applied {_app.Changes.Count} reversible changes.";}catch(Exception ex){OperationStatus.Text=$"Optimization stopped: {ex.Message}";}}
    void Restore_Click(object sender,RoutedEventArgs e){try{var n=_app.Restore();TweaksText.Text=$"{_app.Changes.Count}";OperationStatus.Text=$"Restored {n} tracked changes.";}catch(Exception ex){OperationStatus.Text=$"Restore stopped: {ex.Message}";}}
    void Refresh_Click(object sender,RoutedEventArgs e){RefreshStatus();RefreshMetrics();TweaksText.Text=$"{_app.Changes.Count}";}
    async void Activate_Click(object sender,RoutedEventArgs e){OperationStatus.Text="Contacting license server…";var ok=await _app.ActivateAsync(LicenseEndpoint.Text,LicenseKey.Text.Trim());OperationStatus.Text=ok?"License activated successfully.":"License activation failed.";if(ok){_rolledBack=false;_lastValidation=DateTime.UtcNow;}RefreshStatus();}
    protected override void OnClosed(EventArgs e){_timer.Stop();base.OnClosed(e);}
}
