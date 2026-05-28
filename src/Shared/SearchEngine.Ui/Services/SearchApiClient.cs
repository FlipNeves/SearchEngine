using System.Net.Http.Json;
using System.Text.Json;
using SearchEngine.Shared.Dtos;

namespace SearchEngine.Ui.Services;

public sealed class SearchApiClient : ISearchApi
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public SearchApiClient(HttpClient http) => _http = http;

    public async Task<SearchResponseDto?> SearchAsync(string query, int top, CancellationToken ct)
    {
        var url = $"/search?q={Uri.EscapeDataString(query)}&top={top}";
        var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<SearchResponseDto>(_json, ct);
    }

    public async Task<IReadOnlyList<string>> AutocompleteAsync(string prefix, int top, CancellationToken ct)
    {
        var url = $"/autocomplete?prefix={Uri.EscapeDataString(prefix)}&top={top}";
        var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return Array.Empty<string>();
        var list = await resp.Content.ReadFromJsonAsync<List<string>>(_json, ct);
        return list ?? new List<string>();
    }

    public async Task<IReadOnlyList<SuggestionResultDto>> SuggestAsync(string query, int top, CancellationToken ct)
    {
        var url = $"/suggest?q={Uri.EscapeDataString(query)}&top={top}";
        var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return Array.Empty<SuggestionResultDto>();
        var list = await resp.Content.ReadFromJsonAsync<List<SuggestionResultDto>>(_json, ct);
        return list ?? new List<SuggestionResultDto>();
    }
}
