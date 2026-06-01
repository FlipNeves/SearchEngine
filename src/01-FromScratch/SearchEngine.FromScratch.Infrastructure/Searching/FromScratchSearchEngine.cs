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
    private readonly ISpellCorrector _corrector;

    public FromScratchSearchEngine(
        IInvertedIndexDao index,
        IPhraseIndexDao phrase,
        IPagesRepository pages,
        IIndexStatsDao stats,
        Bm25Scorer scorer,
        IOptions<Bm25Options> options,
        ISpellCorrector corrector)
    {
        _index = index;
        _phrase = phrase;
        _pages = pages;
        _stats = stats;
        _scorer = scorer;
        _options = options.Value;
        _corrector = corrector;
    }

    public async Task<SearchResponseDto> SearchAsync(string query, int top, bool autoCorrect = true, CancellationToken ct = default)
    {
        var queryTokens = Tokenizer.Tokenize(query).ToArray();
        if (queryTokens.Length == 0) return Empty(query);

        var ranking = await RankAsync(queryTokens, top, ct);

        if (autoCorrect && ranking.MissingTerms.Count > 0 && _corrector.IsReady)
        {
            var corrected = TryCorrectTokens(queryTokens, ranking.MissingTerms);
            if (corrected is not null)
            {
                var correctedQuery = string.Join(' ', corrected.Select(t => t.Value));
                var correctedRanking = await RankAsync(corrected, top, ct);

                // Decisão 1b: só auto-corrige se a correção de fato traz resultados.
                if (correctedRanking.Hits.Count > 0)
                    return new SearchResponseDto(correctedQuery, correctedRanking.Hits, new DidYouMean(query, correctedQuery));
            }
        }

        return new SearchResponseDto(query, ranking.Hits, null);
    }

    private async Task<RankResult> RankAsync(Token[] queryTokens, int top, CancellationToken ct)
    {
        var terms = queryTokens.Select(t => t.Value).Distinct(StringComparer.Ordinal).ToArray();
        var termEntries = await _index.FindByTermsAsync(terms, ct);

        var presentTerms = termEntries.Select(e => e.Term).ToHashSet(StringComparer.Ordinal);
        var missingTerms = terms.Where(t => !presentTerms.Contains(t)).ToArray();

        if (termEntries.Count == 0) return new RankResult(Array.Empty<SearchHit>(), missingTerms);

        var corpus = await _stats.GetAsync(ct);
        if (corpus.TotalDocs == 0) return new RankResult(Array.Empty<SearchHit>(), missingTerms);

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

        return new RankResult(hits, missingTerms);

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

    private Token[]? TryCorrectTokens(Token[] tokens, IReadOnlyCollection<string> missingTerms)
    {
        var missing = missingTerms.ToHashSet(StringComparer.Ordinal);
        var corrected = new Token[tokens.Length];
        var anyCorrected = false;

        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (missing.Contains(token.Value))
            {
                var fix = _corrector.Correct(token.Value);
                if (fix is not null && !string.Equals(fix, token.Value, StringComparison.Ordinal))
                {
                    corrected[i] = new Token(fix, token.Start, token.End);
                    anyCorrected = true;
                    continue;
                }
            }
            corrected[i] = token;
        }

        return anyCorrected ? corrected : null;
    }

    private static SearchResponseDto Empty(string query)
        => new(query, Array.Empty<SearchHit>(), null);

    private static string Truncate(string text, int max)
        => string.IsNullOrEmpty(text) || text.Length <= max ? text : text[..max] + "…";

    private readonly record struct RankResult(IReadOnlyList<SearchHit> Hits, IReadOnlyList<string> MissingTerms);
}
