# UndOpti

Windows WPF/.NET 8 performance utility for gaming PCs, focused on Rust. The application is standalone: **no keys, licenses, license server, or activation are required**.

## Features

- WPF dashboard for Windows x64
- Hardware scan: CPU, GPU, RAM, motherboard and driver information
- Live CPU/RAM/Rust monitoring
- Reversible Windows Game Mode / Game DVR / transparency optimization
- Temporary-file cleaner
- DNS cache flush utility
- High-performance power-plan selection
- Rust competitive profile integration
- Persistent change tracker with conflict-safe restore
- Hardware/BIOS analysis and OC/undervolt advisor
- Single-file self-contained Windows EXE publishing

## Safety model

System changes that are tracked store their original and applied values. Restore only runs when the current value still equals the value written by UndOpti. This prevents the optimizer from silently overwriting a manual change made after optimization.

BIOS flashing and blind voltage/clock writes are not performed automatically. Advanced CPU/GPU/RAM tuning must be matched to the exact hardware and tested for stability.

The application does not disable Windows security or attempt to bypass Rust/EAC protections.

## Build

```powershell
dotnet restore src/RustPerformanceSuite/RustPerformanceSuite.csproj
dotnet build src/RustPerformanceSuite/RustPerformanceSuite.csproj -c Release
dotnet publish src/RustPerformanceSuite/RustPerformanceSuite.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```
