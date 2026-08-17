using System.Net.Http.Json;

namespace RustPerformanceSuite.Services;

public sealed class AiOptimizationService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public async Task<string> AskAsync(string endpoint, string prompt, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return "Configure an AI endpoint first.";
        try
        {
            using var response = await _http.PostAsJsonAsync(endpoint, new { prompt }, token);
            if (!response.IsSuccessStatusCode) return $"AI request failed: {(int)response.StatusCode}";
            return await response.Content.ReadAsStringAsync(token);
        }
        catch (Exception ex) { return $"AI request failed: {ex.Message}"; }
    }
}
