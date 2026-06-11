using Microsoft.Extensions.Options;
using SearchEngine.FromScratch.Api.Options;
using SearchEngine.FromScratch.Api.Services;
using SearchEngine.Shared.Domain.Interfaces;
using SearchEngine.Shared.Dtos;

namespace SearchEngine.FromScratch.Api.Endpoints;

public static class SearchEndpoints
{
    private const int FuzzyTriggerThreshold = 3;
    private const int FuzzyMinPrefixLength = 4;

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
            .WithSummary("Sugere completions de prefixo (Trie) e, quando há poucas, correções fuzzy (BK-tree).")
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
            vocabulary.Current,
            prefix.Trim().ToLowerInvariant(),
            top,
            options.Value.MaxSuggestionDistance);

        return Results.Ok(new AutocompleteResponseDto(prefix, suggestions));
    }

    private static IReadOnlyList<AutocompleteSuggestion> BuildSuggestions(
        VocabularySnapshot snapshot, string prefix, int top, int maxDistance)
    {
        var result = new List<AutocompleteSuggestion>(top);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var word in snapshot.Trie.Autocomplete(prefix).Take(top))
            if (seen.Add(word))
                result.Add(new AutocompleteSuggestion(word, IsCorrection: false));

        var needsFuzzy = result.Count < FuzzyTriggerThreshold && prefix.Length >= FuzzyMinPrefixLength;
        if (needsFuzzy)
        {
            var fuzzy = snapshot.BkTree.Search(prefix, maxDistance)
                .Where(m => m.Distance > 0 && !seen.Contains(m.Word))
                .OrderBy(m => m.Distance)
                .ThenByDescending(m => snapshot.Trie.Frequency(m.Word))
                .Select(m => m.Word);

            foreach (var word in fuzzy)
            {
                if (result.Count >= top) break;
                if (seen.Add(word))
                    result.Add(new AutocompleteSuggestion(word, IsCorrection: true));
            }
        }

        return result;
    }
}
