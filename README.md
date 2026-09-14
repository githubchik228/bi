# UndOpti

Windows WPF/.NET 8 performance suite for gaming PCs, focused on Rust. The app is standalone: **no keys, licenses, license server, or activation are required**.

## Included

- Live CPU/RAM/Rust monitoring
- Hardware scan: CPU, GPU, RAM, motherboard and GPU driver
- One-click Windows gaming profile with reversible tracked registry changes
- Rust Competitive profile and session-only AboveNormal process priority
- Temporary-file cleaner and DirectX shader-cache cleaner
- DNS flush and network diagnostics with latency check
- High-performance power plan with saved previous-plan restore
- Startup report without automatically disabling startup programs
- Hardware / BIOS / XMP / EXPO / CPU / GPU tuning advisor
- Performance recommendations based on detected hardware
- Restore of tracked registry changes and saved power-plan state
- Self-contained Windows x64 publishing

## Safety

UndOpti does not flash BIOS firmware, write blind CPU/GPU voltages or clocks, disable Windows security, modify Rust files, or bypass Rust/EAC protections. Advanced tuning is advisory and should be matched to the exact hardware and stability-tested.

Tracked registry changes are restored only when the current value still matches the value UndOpti wrote. This avoids silently overwriting a manual change made later.

## Build

```powershell
dotnet restore src/RustPerformanceSuite/RustPerformanceSuite.csproj
dotnet build src/RustPerformanceSuite/RustPerformanceSuite.csproj -c Release
dotnet publish src/RustPerformanceSuite/RustPerformanceSuite.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```
