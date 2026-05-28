using Microsoft.Extensions.Options;
using MongoDB.Bson;
using SearchEngine.FromScratch.Core.Ranking;
using SearchEngine.FromScratch.Core.Text;
using SearchEngine.FromScratch.Infrastructure.DataModels;
using SearchEngine.FromScratch.Infrastructure.Daos;
using SearchEngine.Shared.Domain.Interfaces;
using SearchEngine.Shared.Dtos;

namespace SearchEngine.FromScratch.Infrastructure.Searching;

public sealed class FromScratchSearchEngine : ISearchEngine
{
    private static readonly int[] PhraseSizes = { 2, 3 };

    private readonly IInvertedIndexDao _index;
    private readonly IPhraseIndexDao _phrase;
    private readonly IPagesRepository _pages;
    private readonly IIndexStatsDao _stats;
    private readonly Bm25Scorer _scorer;
    private readonly Bm25Options _options;

    public FromScratchSearchEngine(
        IInvertedIndexDao index,
        IPhraseIndexDao phrase,
        IPagesRepository pages,
        IIndexStatsDao stats,
        Bm25Scorer scorer,
        IOptions<Bm25Options> options)
    {
        _index = index;
        _phrase = phrase;
        _pages = pages;
        _stats = stats;
        _scorer = scorer;
        _options = options.Value;
    }

    public async Task<SearchResponseDto> SearchAsync(string query, int top, CancellationToken ct = default)
    {
        var queryTokens = Tokenizer.Tokenize(query).ToArray();
        if (queryTokens.Length == 0) return Empty(query);

        var terms = queryTokens.Select(t => t.Value).Distinct(StringComparer.Ordinal).ToArray();
        var termEntries = await _index.FindByTermsAsync(terms, ct);
        if (termEntries.Count == 0) return Empty(query);

        var corpus = await _stats.GetAsync(ct);
        if (corpus.TotalDocs == 0) return Empty(query);

        var candidateIds = termEntries
            .SelectMany(e => e.Postings.Select(p => p.DocId.ToString()))
            .Distinct()
            .ToArray();

        var lengths = await _pages.GetLengthsByIdsAsync(candidateIds, ct);

        var scoreByDoc = new Dictionary<ObjectId, double>();

        Accumulate(termEntries.Select(e => e.Postings), boost: 1.0);

        var phraseKeys = PhraseSizes
            .SelectMany(size => NGram.Generate(queryTokens, size))
            .Select(ng => ng.Phrase)
            .ToArray();

        if (phraseKeys.Length > 0)
        {
            var phraseEntries = await _phrase.FindByPhrasesAsync(phraseKeys, ct);
            Accumulate(phraseEntries.Select(e => e.Postings), boost: _options.PhraseBoost);
        }

        var ranked = scoreByDoc
            .OrderByDescending(kv => kv.Value)
            .Take(top)
            .ToArray();

        var pageIds = ranked.Select(kv => kv.Key.ToString()).ToArray();
        var pageDocs = await _pages.ListByIdsAsync(pageIds, ct);
        var pageById = pageDocs.ToDictionary(p => p.Id, p => p);

        var hits = ranked
            .Where(kv => pageById.ContainsKey(kv.Key.ToString()))
            .Select(kv =>
            {
                var p = pageById[kv.Key.ToString()];
                return new SearchHit(p.Url, p.Title, Truncate(p.Content, 200), kv.Value);
            })
            .ToArray();

        return new SearchResponseDto(query, hits, Array.Empty<string>(), null);

        void Accumulate(IEnumerable<IReadOnlyList<PostingDataModel>> postingLists, double boost)
        {
            foreach (var postings in postingLists)
            {
                var df = postings.Count;
                foreach (var posting in postings)
                {
                    if (!lengths.TryGetValue(posting.DocId.ToString(), out var len)) continue;

                    var titleScore = _scorer.Score(posting.TfTitle, df, corpus.TotalDocs, len.Title, corpus.AvgLengthTitle);
                    var contentScore = _scorer.Score(posting.TfContent, df, corpus.TotalDocs, len.Content, corpus.AvgLengthContent);
                    var combined = boost * (_options.WTitle * titleScore + _options.WContent * contentScore);

                    scoreByDoc.TryGetValue(posting.DocId, out var current);
                    scoreByDoc[posting.DocId] = current + combined;
                }
            }
        }
    }

    private static SearchResponseDto Empty(string query)
        => new(query, Array.Empty<SearchHit>(), Array.Empty<string>(), null);

    private static string Truncate(string text, int max)
        => string.IsNullOrEmpty(text) || text.Length <= max ? text : text[..max] + "…";
}
