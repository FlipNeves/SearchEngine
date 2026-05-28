using MongoDB.Bson;
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
        var terms = Tokenizer
            .Tokenize(query)
            .Select(t => t.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (terms.Length == 0)
            return Empty(query);

        var entries = await _index.FindByTermsAsync(terms, ct);
        if (entries.Count == 0)
            return Empty(query);

        var scoreByDoc = new Dictionary<ObjectId, double>();
        foreach (var entry in entries)
            foreach (var posting in entry.Postings)
            {
                scoreByDoc.TryGetValue(posting.DocId, out var current);
                scoreByDoc[posting.DocId] = current + posting.TfTitle + posting.TfContent;
            }

        var ranked = scoreByDoc
            .OrderByDescending(kv => kv.Value)
            .Take(top)
            .ToArray();

        var pageIds = ranked.Select(kv => kv.Key.ToString()).ToArray();
        var pages = await _pages.ListByIdsAsync(pageIds, ct);
        var pageById = pages.ToDictionary(p => p.Id, p => p);

        var hits = ranked
            .Where(kv => pageById.ContainsKey(kv.Key.ToString()))
            .Select(kv =>
            {
                var p = pageById[kv.Key.ToString()];
                return new SearchHit(p.Url, p.Title, Truncate(p.Content, 200), kv.Value);
            })
            .ToArray();

        return new SearchResponseDto(query, hits, Array.Empty<string>(), null);
    }

    private static SearchResponseDto Empty(string query)
        => new(query, Array.Empty<SearchHit>(), Array.Empty<string>(), null);

    private static string Truncate(string text, int max)
        => string.IsNullOrEmpty(text) || text.Length <= max ? text : text[..max] + "…";
}
