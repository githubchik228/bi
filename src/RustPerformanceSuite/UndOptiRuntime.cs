using System.Diagnostics;
using System.Management;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace RustPerformanceSuite;

public sealed record ChangeRecord(string Id,string Target,string Before,string After,DateTime Utc);
public sealed record BenchmarkResult(double AverageFps,double OnePercentLow,double AverageFrameTimeMs,int Samples);
public sealed record LicenseState(string Key,string Plan,DateTimeOffset ActivatedAt,DateTimeOffset? ExpiresAt,string HardwareId)
{
    public bool Active => ExpiresAt is null || ExpiresAt > DateTimeOffset.UtcNow;
}

public sealed class UndOptiRuntime
{
    public static UndOptiRuntime Instance { get; } = new();
    public string Root { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"UndOpti");
    public string ChangesFile => Path.Combine(Root,"changes.json");
    public string LicenseFile => Path.Combine(Root,"license.json");
    public List<ChangeRecord> Changes { get; private set; } = [];
    public LicenseState? License { get; private set; }
    public string HardwareId { get; }
    private readonly HttpClient http = new(){Timeout=TimeSpan.FromSeconds(8)};
    private UndOptiRuntime(){ Directory.CreateDirectory(Root); HardwareId=CreateHardwareId(); Load(); }

    void Load(){ try{ if(File.Exists(ChangesFile)) Changes=JsonSerializer.Deserialize<List<ChangeRecord>>(File.ReadAllText(ChangesFile))??[]; if(File.Exists(LicenseFile)) License=JsonSerializer.Deserialize<LicenseState>(File.ReadAllText(LicenseFile)); }catch{} }
    void SaveChanges()=>File.WriteAllText(ChangesFile,JsonSerializer.Serialize(Changes,new JsonSerializerOptions{WriteIndented=true}));
    void SaveLicense()=>File.WriteAllText(LicenseFile,JsonSerializer.Serialize(License,new JsonSerializerOptions{WriteIndented=true}));
    static string CreateHardwareId(){ var s=$"{Environment.MachineName}|{Environment.OSVersion}|{Environment.ProcessorCount}"; return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)))[..32]; }

    public async Task<bool> ActivateAsync(string endpoint,string key){
        try{ var r=await http.PostAsJsonAsync(endpoint.TrimEnd('/')+"/v1/license/activate",new{Key=key,HardwareId}); if(!r.IsSuccessStatusCode)return false; var dto=await r.Content.ReadFromJsonAsync<ServerLicense>(); if(dto is null)return false; License=new LicenseState(dto.Key,dto.Plan,dto.ActivatedAt,dto.ExpiresAt,HardwareId); SaveLicense(); return true; }catch{return false;}
    }
    public async Task<bool> ValidateAsync(string endpoint){
        if(License is null)return false; if(!License.Active)return false;
        try{ var r=await http.PostAsJsonAsync(endpoint.TrimEnd('/')+"/v1/license/validate",new{Key=License.Key,HardwareId}); if(!r.IsSuccessStatusCode)return License.Active; var dto=await r.Content.ReadFromJsonAsync<ServerLicense>(); if(dto is null)return License.Active; License=new LicenseState(dto.Key,dto.Plan,dto.ActivatedAt,dto.ExpiresAt,HardwareId); SaveLicense(); return License.Active; }catch{return License.Active;}
    }
    public bool IsLicensed=>License?.Active==true && License.HardwareId==HardwareId;
    public void ApplySafeProfile(){
        SetDword("GameMode",@"Software\Microsoft\GameBar","AutoGameModeEnabled",1);
        SetDword("GameDVR",@"SystemGameConfigStore","GameDVR_Enabled",0);
        SetDword("AppCapture",@"Software\Microsoft\Windows\CurrentVersion\GameDVR","AppCaptureEnabled",0);
        SetDword("Transparency",@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","EnableTransparency",0);
    }
    void SetDword(string id,string path,string name,int value){ using var k=Registry.CurrentUser.CreateSubKey(path,true); if(k is null)return; var before=k.GetValue(name,null)?.ToString()??"<missing>"; var after=value.ToString(); if(before==after)return; k.SetValue(name,value,RegistryValueKind.DWord); Changes.RemoveAll(x=>x.Id==id); Changes.Add(new ChangeRecord(id,$"HKCU\\{path}\\{name}",before,after,DateTime.UtcNow)); SaveChanges(); }
    public int Restore(){ int n=0; foreach(var c in Changes.ToArray().Reverse()){ if(!c.Target.StartsWith("HKCU\\",StringComparison.OrdinalIgnoreCase))continue; var p=c.Target[5..].Split('\\'); if(p.Length<2)continue; var name=p[^1]; var sub=string.Join('\\',p[..^1]); using var k=Registry.CurrentUser.OpenSubKey(sub,true); if(k is null)continue; var now=k.GetValue(name,null)?.ToString()??"<missing>"; if(now!=c.After)continue; if(c.Before=="<missing>")k.DeleteValue(name,false); else if(int.TryParse(c.Before,out var v))k.SetValue(name,v,RegistryValueKind.DWord); Changes.Remove(c); n++; } SaveChanges(); return n; }
    public bool RustRunning()=>Process.GetProcessesByName("RustClient").Length>0;
    public BenchmarkResult Benchmark(IEnumerable<double> frameTimes){ var a=frameTimes.Where(x=>x>0).OrderBy(x=>x).ToArray(); if(a.Length==0)return new(0,0,0,0); var avg=a.Average(); var fps=1000.0/avg; var count=Math.Max(1,(int)Math.Ceiling(a.Length*.01)); var low=1000.0/a[^count..].Average(); return new(fps,low,avg,a.Length); }
    public string HardwareSummary(){ try{ using var s=new ManagementObjectSearcher("SELECT Name,NumberOfCores,NumberOfLogicalProcessors FROM Win32_Processor"); var cpu=s.Get().Cast<ManagementObject>().FirstOrDefault(); return $"CPU: {cpu?["Name"] ?? "Unknown"}\nCores: {cpu?["NumberOfCores"] ?? "?"}\nThreads: {cpu?["NumberOfLogicalProcessors"] ?? "?"}\nRAM: {Math.Round(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes/1073741824d,1)} GB"; }catch{return "Hardware information unavailable.";} }
    sealed record ServerLicense(string Key,string Plan,DateTimeOffset ActivatedAt,DateTimeOffset? ExpiresAt);
}
