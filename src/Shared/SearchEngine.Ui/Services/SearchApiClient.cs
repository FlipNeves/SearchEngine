using System.Net.Http.Json;
using System.Text.Json;
using SearchEngine.Shared.Dtos;

namespace SearchEngine.Ui.Services;

public sealed class SearchApiClient : ISearchApi
{
    private readonly HttpClient _http;
    private readonly EngineSelection _engines;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public SearchApiClient(HttpClient http, EngineSelection engines)
    {
        _http = http;
        _engines = engines;
    }

    public async Task<SearchResponseDto?> SearchAsync(string query, int top, bool correct, CancellationToken ct)
    {
        var url = $"{_engines.Current.BaseUrl}/search?q={Uri.EscapeDataString(query)}&top={top}&correct={(correct ? "true" : "false")}";
        var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<SearchResponseDto>(_json, ct);
    }

    public async Task<AutocompleteResponseDto?> AutocompleteAsync(string prefix, int top, CancellationToken ct)
    {
        var url = $"{_engines.Current.BaseUrl}/autocomplete?prefix={Uri.EscapeDataString(prefix)}&top={top}";
        var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<AutocompleteResponseDto>(_json, ct);
    }
}
