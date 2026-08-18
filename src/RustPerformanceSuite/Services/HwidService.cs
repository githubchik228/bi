using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;
namespace RustPerformanceSuite.Services;
public static class HwidService
{
    public static string GetHardwareId()
    {
        var machineGuid = GetMachineGuid();
        var raw = string.IsNullOrWhiteSpace(machineGuid)
            ? Environment.MachineName
            : machineGuid;
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(
            Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }
    private static string? GetMachineGuid()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Cryptography");
            return key?
                .GetValue("MachineGuid")?
                .ToString();
        }
        catch
        {
            return null;
        }
    }
}
