using System.Net.Http.Json;
using RustPerformanceSuite.Models;

namespace RustPerformanceSuite.Services;

public sealed class RemoteLicenseClient
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };
    public async Task<LicenseInfo?> ActivateAsync(string endpoint, string key, string hardwareId, CancellationToken token = default)
    {
        var response = await _http.PostAsJsonAsync($"{endpoint.TrimEnd('/')}/v1/license/activate", new { Key = key, HardwareId = hardwareId }, token);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<LicenseInfo>(cancellationToken: token);
    }
}