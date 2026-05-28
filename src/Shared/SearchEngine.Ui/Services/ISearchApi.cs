using SearchEngine.Shared.Dtos;

namespace SearchEngine.Ui.Services;

public interface ISearchApi
{
    Task<SearchResponseDto?> SearchAsync(string query, int top, CancellationToken ct);
    Task<IReadOnlyList<string>> AutocompleteAsync(string prefix, int top, CancellationToken ct);
    Task<IReadOnlyList<SuggestionResultDto>> SuggestAsync(string query, int top, CancellationToken ct);
}
