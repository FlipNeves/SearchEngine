using Microsoft.Extensions.Options;
using MongoDB.Bson;
using SearchEngine.FromScratch.Core.Ranking;
using SearchEngine.FromScratch.Core.Text;
using SearchEngine.FromScratch.Infrastructure.Daos;
using SearchEngine.Shared.Domain.Interfaces;
using SearchEngine.Shared.Dtos;

namespace SearchEngine.FromScratch.Infrastructure.Searching;

public sealed class FromScratchSearchEngine : ISearchEngine
{
    private readonly IInvertedIndexDao _index;
    private readonly IPagesRepository _pages;
    private readonly IIndexStatsDao _stats;
    private readonly Bm25Scorer _scorer;
    private readonly Bm25Options _options;

    public FromScratchSearchEngine(
        IInvertedIndexDao index,
        IPagesRepository pages,
        IIndexStatsDao stats,
        Bm25Scorer scorer,
        IOptions<Bm25Options> options)
    {
        _index = index;
        _pages = pages;
        _stats = stats;
        _scorer = scorer;
        _options = options.Value;
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

        var stats = await _stats.GetAsync(ct);
        if (stats.TotalDocs == 0)
            return Empty(query);

        var candidateIds = entries
            .SelectMany(e => e.Postings.Select(p => p.DocId.ToString()))
            .Distinct()
            .ToArray();

        var lengths = await _pages.GetLengthsByIdsAsync(candidateIds, ct);

        var scoreByDoc = new Dictionary<ObjectId, double>();
        foreach (var entry in entries)
        {
            var df = entry.Postings.Count;
            foreach (var posting in entry.Postings)
            {
                if (!lengths.TryGetValue(posting.DocId.ToString(), out var len)) continue;

                var titleScore = _scorer.Score(posting.TfTitle, df, stats.TotalDocs, len.Title, stats.AvgLengthTitle);
                var contentScore = _scorer.Score(posting.TfContent, df, stats.TotalDocs, len.Content, stats.AvgLengthContent);

                scoreByDoc.TryGetValue(posting.DocId, out var current);
                scoreByDoc[posting.DocId] = current + _options.WTitle * titleScore + _options.WContent * contentScore;
            }
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
