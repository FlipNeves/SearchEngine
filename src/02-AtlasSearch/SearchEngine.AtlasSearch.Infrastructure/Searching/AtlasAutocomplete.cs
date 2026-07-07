using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Search;
using SearchEngine.Shared.Dtos;
using SearchEngine.Shared.Persistence.DataModels;
using SearchEngine.Shared.Persistence.Internal;
using SearchEngine.Shared.Text;

namespace SearchEngine.AtlasSearch.Infrastructure.Searching;

public sealed class AtlasAutocomplete
{
    private const int WordSuggestionLimit = 5;

    private readonly IMongoCollection<WebPageDataModel> _pages;
    private readonly AtlasSearchOptions _options;

    public AtlasAutocomplete(IMongoDatabase database, IOptions<AtlasSearchOptions> options)
    {
        _pages = CollectionResolver.Resolve<WebPageDataModel>(database);
        _options = options.Value;
    }

    public async Task<AutocompleteResponseDto> SuggestAsync(string prefix, int top, CancellationToken ct = default)
    {
        var folded = TextFolding.Fold(prefix);
        var maxEdits = MaxEditsFor(folded);
        var fuzzy = maxEdits > 0
            ? new SearchFuzzyOptions { MaxEdits = maxEdits, PrefixLength = _options.FuzzyPrefixLength }
            : null;

        var definition = Builders<WebPageDataModel>.Search.Autocomplete(x => x.Title, prefix, fuzzy: fuzzy);

        var pages = await _pages.Aggregate()
            .Search(definition, indexName: _options.IndexName)
            .Limit(top * 2)
            .Project<TitleOnly>(Builders<WebPageDataModel>.Projection
                .Exclude("_id")
                .Include(x => x.Title))
            .ToListAsync(ct);

        var titles = pages.Select(p => p.Title).Where(t => t.Length > 0).ToArray();

        var suggestions = new List<AutocompleteSuggestion>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var word in SynthesizeWords(titles, folded, maxEdits, _options.FuzzyPrefixLength))
        {
            if (suggestions.Count >= WordSuggestionLimit) break;
            if (seen.Add(word.Text)) suggestions.Add(word);
        }

        foreach (var title in titles)
        {
            if (suggestions.Count >= top) break;
            if (seen.Add(title)) suggestions.Add(new AutocompleteSuggestion(title, IsCorrection: false));
        }

        return new AutocompleteResponseDto(prefix, suggestions);
    }

    private int MaxEditsFor(string foldedPrefix) => foldedPrefix.Length switch
    {
        < 3 => 0,
        < 5 => Math.Min(1, _options.FuzzyMaxEdits),
        _ => _options.FuzzyMaxEdits
    };

    private static IEnumerable<AutocompleteSuggestion> SynthesizeWords(
        IReadOnlyList<string> titles, string foldedPrefix, int maxEdits, int prefixLength)
    {
        if (foldedPrefix.Length == 0) return [];

        var anchor = foldedPrefix[..Math.Min(prefixLength, foldedPrefix.Length)];
        var candidates = new Dictionary<string, (string Word, int Distance, int Count)>(StringComparer.Ordinal);
        foreach (var title in titles)
            foreach (var token in TextFolding.SplitWords(title))
            {
                var tokenFold = TextFolding.Fold(token);
                if (tokenFold.Length < foldedPrefix.Length) continue;
                if (!tokenFold.StartsWith(anchor, StringComparison.Ordinal)) continue;

                var window = tokenFold[..foldedPrefix.Length];
                var distance = DamerauLevenshtein.Distance(window, foldedPrefix);
                if (distance > maxEdits) continue;

                if (candidates.TryGetValue(tokenFold, out var existing))
                    candidates[tokenFold] = (existing.Word, Math.Min(existing.Distance, distance), existing.Count + 1);
                else
                    candidates[tokenFold] = (token.ToLowerInvariant(), distance, 1);
            }

        return candidates.Values
            .OrderBy(c => c.Distance)
            .ThenByDescending(c => c.Count)
            .ThenBy(c => c.Word.Length)
            .Select(c => new AutocompleteSuggestion(c.Word, IsCorrection: c.Distance > 0));
    }

    private sealed class TitleOnly
    {
        public string Title { get; set; } = string.Empty;
    }
}
