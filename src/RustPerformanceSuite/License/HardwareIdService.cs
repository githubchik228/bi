using System.Security.Cryptography;
using System.Text;

namespace RustPerformanceSuite.License;

public sealed class HardwareIdService
{
    public string GetHardwareId()
    {
        var seed = $"{Environment.MachineName}|{Environment.OSVersion.VersionString}|{Environment.ProcessorCount}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed)))[..32];
    }
}
