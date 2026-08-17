using System.Security.Cryptography;
using System.Text;

namespace RustPerformanceSuite.Services;

public sealed class HardwareIdService
{
    public string GetHardwareId()
    {
        var source = $"{Environment.MachineName}|{Environment.OSVersion.VersionString}|{Environment.ProcessorCount}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
    }
}