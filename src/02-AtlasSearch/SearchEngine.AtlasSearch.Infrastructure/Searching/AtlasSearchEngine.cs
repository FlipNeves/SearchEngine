using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Search;
using SearchEngine.Shared.Domain.Interfaces;
using SearchEngine.Shared.Dtos;
using SearchEngine.Shared.Persistence.DataModels;
using SearchEngine.Shared.Persistence.Internal;
using SearchEngine.Shared.Text;

namespace SearchEngine.AtlasSearch.Infrastructure.Searching;

public sealed class AtlasSearchEngine : ISearchEngine
{
    private readonly IMongoCollection<WebPageDataModel> _pages;
    private readonly AtlasSearchOptions _options;
    private readonly LanguageDetector _language;

    public AtlasSearchEngine(IMongoDatabase database, IOptions<AtlasSearchOptions> options, LanguageDetector language)
    {
        _pages = CollectionResolver.Resolve<WebPageDataModel>(database);
        _options = options.Value;
        _language = language;
    }

    public async Task<SearchResponseDto> SearchAsync(string query, int top, bool autoCorrect = true, CancellationToken ct = default)
    {
        var search = Builders<WebPageDataModel>.Search;
        var score = Builders<WebPageDataModel>.SearchScore;
        var fuzzy = autoCorrect
            ? new SearchFuzzyOptions { MaxEdits = _options.FuzzyMaxEdits, PrefixLength = _options.FuzzyPrefixLength }
            : null;

        var clauses = new List<SearchDefinition<WebPageDataModel>>
        {
            search.Text(x => x.Title, query, score: score.Boost(_options.WTitle)),
            search.Text(x => x.Content, query)
        };

        if (fuzzy is not null)
        {
            clauses.Add(search.Text(x => x.Title, query, fuzzy: fuzzy,
                score: score.Boost(_options.WTitle * _options.FuzzyFallbackBoost)));
            clauses.Add(search.Text(x => x.Content, query, fuzzy: fuzzy,
                score: score.Boost(_options.FuzzyFallbackBoost)));
        }

        if (_options.PhraseBoost > 0)
        {
            clauses.Add(search.Phrase(x => x.Title, query, new SearchPhraseOptions<WebPageDataModel>
            {
                Slop = _options.PhraseSlop,
                Score = score.Boost(_options.WTitle * _options.PhraseBoost)
            }));
            clauses.Add(search.Phrase(x => x.Content, query, new SearchPhraseOptions<WebPageDataModel>
            {
                Slop = _options.PhraseSlop,
                Score = score.Boost(_options.PhraseBoost)
            }));
        }

        SearchDefinition<WebPageDataModel> definition = search.Compound().Should(clauses);

        if (_options.LanguageBoost > 0)
        {
            var queryLanguage = _language.Detect(query);
            if (queryLanguage.Confident)
                definition = search.Compound()
                    .Must(definition)
                    .Should(search.Equals(x => x.Language, queryLanguage.Language, score.Boost(_options.LanguageBoost)));
        }
        var highlight = new SearchHighlightOptions<WebPageDataModel>(x => x.Content);

        var pages = await _pages.Aggregate()
            .Search(definition, highlight, _options.IndexName)
            .Limit(top)
            .Project<ScoredPage>(Builders<WebPageDataModel>.Projection
                .Exclude("_id")
                .Include(x => x.Url)
                .Include(x => x.Title)
                .Include(x => x.Content)
                .MetaSearchScore("score")
                .MetaSearchHighlights("highlights"))
            .ToListAsync(ct);

        var results = pages
            .Select(p => new SearchHit(p.Url, p.Title, BuildPreview(p), p.Score))
            .ToArray();

        if (autoCorrect && results.Length > 0)
        {
            var corrected = TryCorrectFromHighlights(query, pages);
            if (corrected is not null)
                return new SearchResponseDto(corrected, results, new DidYouMean(query, corrected));
        }

        return new SearchResponseDto(query, results, null);
    }

    private static string? TryCorrectFromHighlights(string query, IReadOnlyList<ScoredPage> pages)
    {
        var hitWordsByFold = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var page in pages)
            foreach (var highlight in page.Highlights)
                foreach (var text in highlight.Texts)
                {
                    if (text.Type != HighlightTextType.Hit) continue;
                    foreach (var word in TextFolding.SplitWords(text.Value))
                        hitWordsByFold.TryAdd(TextFolding.Fold(word), word.ToLowerInvariant());
                }

        if (hitWordsByFold.Count == 0) return null;

        var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var anyCorrected = false;

        for (var i = 0; i < words.Length; i++)
        {
            var folded = TextFolding.Fold(string.Concat(TextFolding.SplitWords(words[i])));
            if (folded.Length < 2 || hitWordsByFold.ContainsKey(folded)) continue;

            string? best = null;
            var bestDistance = int.MaxValue;
            foreach (var (candidateFold, candidate) in hitWordsByFold)
            {
                var distance = DamerauLevenshtein.Distance(folded, candidateFold);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            if (best is not null && bestDistance <= 2 && bestDistance < folded.Length)
            {
                words[i] = best;
                anyCorrected = true;
            }
        }

        return anyCorrected ? string.Join(' ', words) : null;
    }

    private static string BuildPreview(ScoredPage page)
    {
        if (page.Highlights.Length == 0)
            return Truncate(CollapseWhitespace(page.Content), 200);

        var passages = page.Highlights
            .OrderByDescending(h => h.Score)
            .Take(2)
            .Select(h => CollapseWhitespace(string.Concat(h.Texts.Select(t => t.Value))));

        return Truncate(string.Join(" … ", passages), 300);
    }

    private static string CollapseWhitespace(string text)
        => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max] + "…";

    private sealed class ScoredPage
    {
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public double Score { get; set; }
        public SearchHighlight[] Highlights { get; set; } = [];
    }
}
