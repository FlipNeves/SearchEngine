using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SearchEngine.Shared.Dtos;
using SearchEngine.Shared.Persistence.DataModels;
using SearchEngine.Shared.Persistence.Internal;

namespace SearchEngine.AtlasSearch.Infrastructure.Searching;

public sealed class AtlasAutocomplete
{
    private readonly IMongoCollection<WebPageDataModel> _pages;
    private readonly AtlasSearchOptions _options;

    public AtlasAutocomplete(IMongoDatabase database, IOptions<AtlasSearchOptions> options)
    {
        _pages = CollectionResolver.Resolve<WebPageDataModel>(database);
        _options = options.Value;
    }

    public async Task<AutocompleteResponseDto> SuggestAsync(string prefix, int top, CancellationToken ct = default)
    {
        var definition = Builders<WebPageDataModel>.Search.Autocomplete(x => x.Title, prefix);

        var pages = await _pages.Aggregate()
            .Search(definition, indexName: _options.IndexName)
            .Limit(top * 2)
            .Project<TitleOnly>(Builders<WebPageDataModel>.Projection
                .Exclude("_id")
                .Include(x => x.Title))
            .ToListAsync(ct);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var suggestions = pages
            .Select(p => p.Title)
            .Where(t => t.Length > 0 && seen.Add(t))
            .Take(top)
            .Select(t => new AutocompleteSuggestion(t, IsCorrection: false))
            .ToArray();

        return new AutocompleteResponseDto(prefix, suggestions);
    }

    private sealed class TitleOnly
    {
        public string Title { get; set; } = string.Empty;
    }
}
