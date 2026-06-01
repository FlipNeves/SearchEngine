using SearchEngine.Shared.Dtos;

namespace SearchEngine.Ui.Services;

public interface ISearchApi
{
    Task<SearchResponseDto?> SearchAsync(string query, int top, bool correct, CancellationToken ct);
    Task<IReadOnlyList<string>> AutocompleteAsync(string prefix, int top, CancellationToken ct);
}
