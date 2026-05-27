using SearchEngine.FromScratch.Core.Text;
using SearchEngine.FromScratch.Infrastructure.Daos;
using SearchEngine.Shared.Domain.Interfaces;
using SearchEngine.Shared.Dtos;

namespace SearchEngine.FromScratch.Infrastructure.Searching;

public sealed class FromScratchSearchEngine : ISearchEngine
{
    private readonly IInvertedIndexDao _index;
    private readonly IPagesRepository _pages;

    public FromScratchSearchEngine(IInvertedIndexDao index, IPagesRepository pages)
    {
        _index = index;
        _pages = pages;
    }

    public async Task<SearchResponseDto> SearchAsync(string query, int top, CancellationToken ct = default)
    {
        var tokens = Tokenizer
            .Tokenize(query)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (tokens.Length == 0)
            return Empty(query);

        var entries = await _index.FindByWordsAsync(tokens, ct);
        if (entries.Count == 0)
            return Empty(query);

        var pageIds = entries
            .SelectMany(e => e.PageIds.Select(o => o.ToString()))
            .Distinct()
            .ToArray();

        var pages = await _pages.ListByIdsAsync(pageIds, ct);

        var hits = pages
            .Take(top)
            .Select(p => new SearchHit(p.Url, p.Title, Truncate(p.Content, 200), 0.0))
            .ToArray();

        return new SearchResponseDto(query, hits, Array.Empty<string>(), null);
    }

    private static SearchResponseDto Empty(string query)
        => new(query, Array.Empty<SearchHit>(), Array.Empty<string>(), null);

    private static string Truncate(string text, int max)
        => string.IsNullOrEmpty(text) || text.Length <= max ? text : text[..max] + "…";
}
