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
            search.Text(x => x.Title, query, fuzzy: fuzzy, score: score.Boost(_options.WTitle)),
            search.Text(x => x.Content, query, fuzzy: fuzzy)
        };

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

        return new SearchResponseDto(query, results, null);
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
