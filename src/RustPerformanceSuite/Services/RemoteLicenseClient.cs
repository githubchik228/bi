using System.Net.Http.Json;
using RustPerformanceSuite.Models;

namespace RustPerformanceSuite.Services;

public sealed class RemoteLicenseClient
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public async Task<LicenseInfo?> ActivateAsync(string endpoint, string key, string hardwareId, CancellationToken token = default)
    {
        using var response = await _http.PostAsJsonAsync($"{endpoint.TrimEnd('/')}/api/license/activate", new { Key = key, HardwareId = hardwareId }, token);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<LicenseInfo>(cancellationToken: token);
    }

    public async Task<LicenseInfo?> ValidateAsync(string endpoint, string key, string hardwareId, CancellationToken token = default)
    {
        using var response = await _http.PostAsJsonAsync($"{endpoint.TrimEnd('/')}/api/license/validate", new { Key = key, HardwareId = hardwareId }, token);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<LicenseInfo>(cancellationToken: token);
    }
}
