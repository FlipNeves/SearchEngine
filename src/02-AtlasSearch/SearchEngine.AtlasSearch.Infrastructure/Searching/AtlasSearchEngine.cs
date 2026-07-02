using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Search;
using SearchEngine.Shared.Domain.Interfaces;
using SearchEngine.Shared.Dtos;
using SearchEngine.Shared.Persistence.DataModels;
using SearchEngine.Shared.Persistence.Internal;

namespace SearchEngine.AtlasSearch.Infrastructure.Searching;

public sealed class AtlasSearchEngine : ISearchEngine
{
    private readonly IMongoCollection<WebPageDataModel> _pages;
    private readonly AtlasSearchOptions _options;

    public AtlasSearchEngine(IMongoDatabase database, IOptions<AtlasSearchOptions> options)
    {
        _pages = CollectionResolver.Resolve<WebPageDataModel>(database);
        _options = options.Value;
    }

    public async Task<SearchResponseDto> SearchAsync(string query, int top, bool autoCorrect = true, CancellationToken ct = default)
    {
        var search = Builders<WebPageDataModel>.Search;
        var fuzzy = autoCorrect
            ? new SearchFuzzyOptions { MaxEdits = _options.FuzzyMaxEdits, PrefixLength = _options.FuzzyPrefixLength }
            : null;

        var definition = search.Compound().Should(
            search.Text(x => x.Title, query, fuzzy: fuzzy,
                score: Builders<WebPageDataModel>.SearchScore.Boost(_options.WTitle)),
            search.Text(x => x.Content, query, fuzzy: fuzzy));

        var pages = await _pages.Aggregate()
            .Search(definition, indexName: _options.IndexName)
            .Limit(top)
            .Project<ScoredPage>(Builders<WebPageDataModel>.Projection
                .Exclude("_id")
                .Include(x => x.Url)
                .Include(x => x.Title)
                .Include(x => x.Content)
                .MetaSearchScore("score"))
            .ToListAsync(ct);

        var results = pages
            .Select(p => new SearchHit(p.Url, p.Title, Truncate(p.Content, 200), p.Score))
            .ToArray();

        return new SearchResponseDto(query, results, null);
    }

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max] + "…";

    private sealed class ScoredPage
    {
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public double Score { get; set; }
    }
}
