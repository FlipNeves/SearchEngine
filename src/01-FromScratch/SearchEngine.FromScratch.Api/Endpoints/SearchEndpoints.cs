using Microsoft.Extensions.Options;
using SearchEngine.FromScratch.Api.Options;
using SearchEngine.FromScratch.Api.Services;
using SearchEngine.Shared.Domain.Interfaces;
using SearchEngine.Shared.Dtos;

namespace SearchEngine.FromScratch.Api.Endpoints;

public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/search", SearchAsync)
            .WithName("Search")
            .WithTags("Search")
            .WithSummary("Busca páginas e auto-corrige termos fora do vocabulário.")
            .WithDescription("Tokeniza `q`, ranqueia por BM25 + phrase boost. Se algum token não existe no índice e a correção tem resultados, busca a correção e retorna `didYouMean`. Passe `correct=false` para desligar a auto-correção (usado pelo link \"buscar pelo original\").")
            .Produces<SearchResponseDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/autocomplete", Autocomplete)
            .WithName("Autocomplete")
            .WithTags("Search")
            .WithSummary("Search-as-you-type híbrido: palavras do vocabulário (com tolerância a typo via caminhada fuzzy na Trie) seguidas de títulos de páginas. `isCorrection` marca palavras cujo prefixo digitado precisou de correção.")
            .Produces<AutocompleteResponseDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> SearchAsync(
        string q,
        ISearchEngine searchEngine,
        CancellationToken ct,
        int top = 20,
        bool correct = true)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Results.Problem("query parameter 'q' is required", statusCode: 400);

        var response = await searchEngine.SearchAsync(q, top, correct, ct);
        return Results.Ok(response);
    }

    private static IResult Autocomplete(
        string prefix,
        VocabularyIndex vocabulary,
        IOptions<TrieRefreshOptions> options,
        int top = 10)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return Results.Problem("query parameter 'prefix' is required", statusCode: 400);

        if (!vocabulary.IsReady)
            return Results.Problem("index is still loading, try again in a few seconds", statusCode: 503);

        var suggestions = BuildSuggestions(
            vocabulary.Current, prefix.Trim(), top, options.Value.MaxSuggestionDistance);

        return Results.Ok(new AutocompleteResponseDto(prefix, suggestions));
    }

    private const int WordSuggestionLimit = 5;

    private static IReadOnlyList<AutocompleteSuggestion> BuildSuggestions(
        VocabularySnapshot snapshot, string prefix, int top, int maxSuggestionDistance)
    {
        var folded = TextFolding.Fold(prefix);
        if (folded.Length == 0) return Array.Empty<AutocompleteSuggestion>();

        var maxEdits = folded.Length switch
        {
            < 3 => 0,
            < 5 => Math.Min(1, maxSuggestionDistance),
            _ => maxSuggestionDistance
        };

        var words = snapshot.Trie.FuzzyAutocomplete(folded, maxEdits);

        var suggestions = new List<AutocompleteSuggestion>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (word, distance) in words)
        {
            if (suggestions.Count >= WordSuggestionLimit) break;
            if (seen.Add(word))
                suggestions.Add(new AutocompleteSuggestion(word, IsCorrection: distance > 0));
        }

        var titles = MatchTitles(snapshot.Titles, folded);
        if (titles.Count == 0 && words.Count > 0)
            titles = MatchTitles(snapshot.Titles, words[0].Word);

        foreach (var title in titles)
        {
            if (suggestions.Count >= top) break;
            if (seen.Add(title))
                suggestions.Add(new AutocompleteSuggestion(title, IsCorrection: false));
        }

        return suggestions;
    }

    private static IReadOnlyList<string> MatchTitles(IReadOnlyList<TitleEntry> titles, string foldedPrefix)
        => titles
            .Select(t => (t.Title, Rank: MatchRank(t.Folded, foldedPrefix)))
            .Where(x => x.Rank >= 0)
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Title.Length)
            .Select(x => x.Title)
            .ToArray();

    private static int MatchRank(string foldedTitle, string foldedPrefix)
    {
        if (foldedTitle.StartsWith(foldedPrefix, StringComparison.Ordinal)) return 0;

        var index = foldedTitle.IndexOf(foldedPrefix, StringComparison.Ordinal);
        while (index > 0)
        {
            if (!char.IsLetterOrDigit(foldedTitle[index - 1])) return 1;
            index = foldedTitle.IndexOf(foldedPrefix, index + 1, StringComparison.Ordinal);
        }

        return -1;
    }
}
