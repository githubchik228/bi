# Rust Performance Suite

Windows WPF/.NET 8 performance utility for Rust with reversible system changes and server-backed time-limited licenses.

## Current implementation

- WPF dashboard for Windows x64
- License server with 1d / 7d / 30d / 1y / lifetime plans
- HWID binding
- Automatic rollback when a locally stored license expires
- Persistent change tracker with conflict-safe restore
- Reversible power-plan optimization
- Reversible Windows Game Mode / Game DVR / transparency tweaks
- Rust installation/process detection
- Basic live CPU and RAM monitoring
- GitHub Actions self-contained Windows EXE publishing

## License server

Run:

```powershell
$env:RPS_ADMIN_TOKEN = "change-this-secret"
dotnet run --project src/LicenseServer/LicenseServer.csproj
```

Create a key:

```powershell
curl -X POST http://localhost:5000/v1/admin/keys -H "Authorization: Bearer change-this-secret" -H "Content-Type: application/json" -d '{"plan":"30d"}'
```

The client sends the machine HWID to `/v1/license/activate`. The server binds a key to the first HWID that activates it.

## Safety model

Every system change stores its original and applied values. Restore only runs when the current value still equals the value written by the optimizer. This prevents the optimizer from silently overwriting a change made manually after optimization.

The application does not disable Windows security or attempt to bypass Rust/EAC protections.

## Build

```powershell
dotnet restore bi.sln
dotnet build bi.sln -c Release
dotnet publish src/RustPerformanceSuite/RustPerformanceSuite.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```
